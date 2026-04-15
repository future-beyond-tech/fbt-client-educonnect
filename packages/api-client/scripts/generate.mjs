import { spawn } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import { mkdir, rm, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const packageDir = path.resolve(__dirname, "..");
const repoRoot = path.resolve(packageDir, "..", "..");
const generatedDir = path.join(packageDir, "src", "generated");
const openApiJsonPath = path.join(generatedDir, "openapi.json");
const openApiSchemaPath = path.join(generatedDir, "schema.ts");
const apiProjectPath = path.join(repoRoot, "apps", "api", "src", "EduConnect.Api", "EduConnect.Api.csproj");
const defaultApiPort = process.env.API_PORT ?? "5000";
const temporaryApiPort = process.env.EDUCONNECT_OPENAPI_PORT ?? "5051";
const defaultOpenApiUrl =
  process.env.EDUCONNECT_OPENAPI_URL ?? `http://127.0.0.1:${defaultApiPort}/openapi/v1.json`;

function loadDotEnvFile(filePath, env) {
  if (!existsSync(filePath)) {
    return;
  }

  const lines = readFileSync(filePath, "utf8").split(/\r?\n/);
  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith("#")) {
      continue;
    }

    const separatorIndex = trimmed.indexOf("=");
    if (separatorIndex <= 0) {
      continue;
    }

    const key = trimmed.slice(0, separatorIndex).trim();
    if (!key || env[key]) {
      continue;
    }

    let value = trimmed.slice(separatorIndex + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }

    env[key] = value;
  }
}

function resolveGeneratorEnvironment() {
  const env = { ...process.env };

  loadDotEnvFile(path.join(repoRoot, ".env"), env);

  const dbMode = env.EDUCONNECT_DB_MODE ?? env.DB_MODE ?? "docker";
  const postgresHostPort =
    env.POSTGRES_HOST_PORT ?? (dbMode === "local" ? "5432" : dbMode === "docker" ? "5433" : "remote");

  if (!env.DATABASE_URL) {
    if (dbMode === "docker") {
      env.DATABASE_URL = `postgresql://educonnect:educonnect_dev@localhost:${postgresHostPort}/educonnect`;
    } else if (dbMode === "local") {
      const localDbUser = env.LOCAL_DB_USER ?? "educonnect";
      const localDbPassword = env.LOCAL_DB_PASSWORD ?? "educonnect_dev";
      const localDbName = env.LOCAL_DB_NAME ?? "educonnect";
      env.DATABASE_URL = `postgresql://${localDbUser}:${localDbPassword}@localhost:${postgresHostPort}/${localDbName}`;
    }
  }

  env.EDUCONNECT_DB_MODE = dbMode;
  env.POSTGRES_HOST_PORT = postgresHostPort;
  env.ASPNETCORE_ENVIRONMENT ??= "Development";
  env.API_PORT = temporaryApiPort;
  env.ASPNETCORE_URLS ??= `http://localhost:${env.API_PORT}`;
  env.JWT_SECRET ??= "dev-secret-key-minimum-64-characters-long-for-hmac-sha256-signing-requirement";
  env.JWT_ISSUER ??= "educonnect-api";
  env.JWT_AUDIENCE ??= "educonnect-client";
  env.PIN_MIN_LENGTH ??= "4";
  env.PIN_MAX_LENGTH ??= "6";
  env.CORS_ALLOWED_ORIGINS ??= "http://localhost:3000";
  env.RATE_LIMIT_API_PER_USER_PER_MINUTE ??= "60";
  env.NEXT_PUBLIC_APP_URL ??= "http://localhost:3000";
  env.RESEND_API_KEY ??= "dev-resend-api-key";
  env.RESEND_FROM_EMAIL ??= "EduConnect <no-reply@example.com>";

  return env;
}

function buildOpenApiUrl(port) {
  return `http://127.0.0.1:${port}/openapi/v1.json`;
}

async function fetchOpenApiDocument(openApiUrl) {
  const response = await fetch(openApiUrl);
  if (!response.ok) {
    throw new Error(`OpenAPI endpoint returned ${response.status} ${response.statusText}`);
  }

  return await response.text();
}

async function waitForOpenApi(childProcess, openApiUrl) {
  const deadline = Date.now() + 90_000;
  let lastError;

  while (Date.now() < deadline) {
    if (childProcess?.exitCode != null) {
      throw new Error(`API process exited before OpenAPI became available (exit ${childProcess.exitCode}).`);
    }

    try {
      return await fetchOpenApiDocument(openApiUrl);
    } catch (error) {
      lastError = error;
      await new Promise((resolve) => setTimeout(resolve, 1_000));
    }
  }

  throw lastError ?? new Error("Timed out waiting for OpenAPI endpoint.");
}

function spawnApi(env) {
  const command = process.platform === "win32" ? "dotnet.exe" : "dotnet";
  const args = ["run", "--no-build", "--project", apiProjectPath];
  const child = spawn(command, args, {
    cwd: repoRoot,
    env,
    stdio: ["ignore", "pipe", "pipe"],
  });

  child.stdout.on("data", (chunk) => {
    process.stdout.write(chunk);
  });

  child.stderr.on("data", (chunk) => {
    process.stderr.write(chunk);
  });

  return child;
}

async function runOpenApiGenerator() {
  const command = process.platform === "win32" ? "pnpm.cmd" : "pnpm";
  const args = ["exec", "openapi-typescript", openApiJsonPath, "-o", openApiSchemaPath];

  await new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: packageDir,
      env: process.env,
      stdio: "inherit",
    });

    child.on("exit", (code) => {
      if (code === 0) {
        resolve();
        return;
      }

      reject(new Error(`openapi-typescript exited with code ${code ?? "unknown"}.`));
    });

    child.on("error", reject);
  });
}

async function main() {
  await mkdir(generatedDir, { recursive: true });

  let apiProcess = null;
  try {
    let openApiDocument;
    try {
      openApiDocument = await fetchOpenApiDocument(defaultOpenApiUrl);
    } catch {
      const env = resolveGeneratorEnvironment();
      const temporaryOpenApiUrl = buildOpenApiUrl(env.API_PORT);

      try {
        openApiDocument = await fetchOpenApiDocument(temporaryOpenApiUrl);
      } catch {
        apiProcess = spawnApi(env);
        openApiDocument = await waitForOpenApi(apiProcess, temporaryOpenApiUrl);
      }
    }

    await writeFile(openApiJsonPath, `${openApiDocument}\n`, "utf8");
    await runOpenApiGenerator();
    await rm(path.join(generatedDir, ".gitkeep"), { force: true });
  } finally {
    if (apiProcess && apiProcess.exitCode == null) {
      await new Promise((resolve) => {
        const forceKillTimer = setTimeout(() => {
          if (apiProcess.exitCode == null) {
            apiProcess.kill("SIGKILL");
          }
        }, 5_000);

        apiProcess.once("exit", () => {
          clearTimeout(forceKillTimer);
          resolve();
        });

        apiProcess.kill("SIGINT");
      });
    }
  }
}

await main();
