const sharedConfig = require("./packages/config/eslint");

module.exports = {
  root: true,
  ...sharedConfig,
  settings: {
    next: {
      rootDir: ["apps/web/"],
    },
  },
  rules: {
    ...sharedConfig.rules,
    "@next/next/no-html-link-for-pages": "off",
    "no-unused-vars": "off",
    "@typescript-eslint/no-unused-vars": [
      "error",
      {
        argsIgnorePattern: "^_",
      },
    ],
    "@typescript-eslint/explicit-function-return-types": "off",
    "@typescript-eslint/no-empty-object-type": "off",
  },
};
