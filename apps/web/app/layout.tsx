import type { Metadata, Viewport } from "next";
import { Manrope, Space_Grotesk } from "next/font/google";
import { headers } from "next/headers";
import { APP_NAME } from "@/lib/constants";
import { validateEnv } from "@/lib/validate-env";
import { AuthProvider } from "@/providers/auth-provider";
import { ThemeProvider } from "@/providers/theme-provider";
import { ServiceWorkerRegistrar } from "@/components/pwa/sw-registrar";
import { InstallPrompt } from "@/components/pwa/install-prompt";
import "@/app/globals.css";

validateEnv();

const manrope = Manrope({
  subsets: ["latin"],
  display: "swap",
  variable: "--font-sans",
});

const spaceGrotesk = Space_Grotesk({
  subsets: ["latin"],
  display: "swap",
  variable: "--font-display",
});

export const metadata: Metadata = {
  title: {
    default: APP_NAME,
    template: `%s | ${APP_NAME}`,
  },
  description: "School communication platform — attendance, homework, and notices",
  manifest: "/manifest.json",
  appleWebApp: {
    capable: true,
    statusBarStyle: "default",
    title: APP_NAME,
  },
  formatDetection: {
    telephone: false,
  },
  icons: {
    icon: [
      { url: "/icon-192x192.png", sizes: "192x192", type: "image/png" },
      { url: "/icon-512x512.png", sizes: "512x512", type: "image/png" },
    ],
    apple: [{ url: "/apple-touch-icon.png", sizes: "180x180" }],
    shortcut: "/favicon.ico",
  },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  maximumScale: 1,
  userScalable: false,
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#1F3C5F" },
    { media: "(prefers-color-scheme: dark)", color: "#0F1320" },
  ],
  viewportFit: "cover",
};

const themeScript = `
  (function () {
    try {
      var storageKey = "educonnect-theme";
      var storedTheme = window.localStorage.getItem(storageKey);
      var theme =
        storedTheme === "dark" || storedTheme === "light"
          ? storedTheme
          : window.matchMedia("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
      var root = document.documentElement;
      root.dataset.theme = theme;
      root.style.colorScheme = theme;
      var themeColorMeta = document.querySelector('meta[name="theme-color"]');
      if (themeColorMeta) {
        themeColorMeta.setAttribute(
          "content",
          theme === "dark" ? "#0F1320" : "#1F3C5F"
        );
      }
    } catch (error) {
      void error;
    }
  })();
`;

export default async function RootLayout({
  children,
}: {
  children: React.ReactNode;
}): Promise<React.ReactElement> {
  const nonce = (await headers()).get("x-nonce") ?? undefined;

  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script nonce={nonce} dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body className={`${manrope.variable} ${spaceGrotesk.variable} antialiased`}>
        <ThemeProvider>
          <AuthProvider>
            {children}
          </AuthProvider>
        </ThemeProvider>
        <ServiceWorkerRegistrar />
        <InstallPrompt />
      </body>
    </html>
  );
}
