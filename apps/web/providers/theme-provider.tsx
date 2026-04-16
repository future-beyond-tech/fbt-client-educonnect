"use client";

import * as React from "react";

export type Theme = "light" | "dark";

interface ThemeContextValue {
  theme: Theme;
  isHydrated: boolean;
  setTheme: (theme: Theme) => void;
  toggleTheme: () => void;
}

const STORAGE_KEY = "educonnect-theme";
const LIGHT_THEME_COLOR = "#1F3C5F";
const DARK_THEME_COLOR = "#0F1320";

const ThemeContext = React.createContext<ThemeContextValue | undefined>(
  undefined
);

function getSystemTheme(): Theme {
  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function getStoredTheme(): Theme | null {
  try {
    const storedTheme = window.localStorage.getItem(STORAGE_KEY);
    return storedTheme === "dark" || storedTheme === "light"
      ? storedTheme
      : null;
  } catch {
    return null;
  }
}

function applyTheme(theme: Theme): void {
  const root = document.documentElement;
  root.dataset.theme = theme;
  root.style.colorScheme = theme;

  const themeColorMeta = document.querySelector<HTMLMetaElement>(
    'meta[name="theme-color"]'
  );

  if (themeColorMeta) {
    themeColorMeta.setAttribute(
      "content",
      theme === "dark" ? DARK_THEME_COLOR : LIGHT_THEME_COLOR
    );
  }
}

export function ThemeProvider({
  children,
}: {
  children: React.ReactNode;
}): React.ReactElement {
  const [theme, setThemeState] = React.useState<Theme>("light");
  const [isHydrated, setIsHydrated] = React.useState(false);

  React.useEffect(() => {
    const nextTheme = getStoredTheme() ?? getSystemTheme();
    setThemeState(nextTheme);
    applyTheme(nextTheme);
    setIsHydrated(true);
  }, []);

  const setTheme = React.useCallback((nextTheme: Theme): void => {
    setThemeState(nextTheme);
    applyTheme(nextTheme);

    try {
      window.localStorage.setItem(STORAGE_KEY, nextTheme);
    } catch {
      // Ignore storage write failures and still apply the theme for this session.
    }
  }, []);

  const toggleTheme = React.useCallback((): void => {
    setTheme(theme === "dark" ? "light" : "dark");
  }, [setTheme, theme]);

  const value = React.useMemo<ThemeContextValue>(
    () => ({
      theme,
      isHydrated,
      setTheme,
      toggleTheme,
    }),
    [theme, isHydrated, setTheme, toggleTheme]
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme(): ThemeContextValue {
  const context = React.useContext(ThemeContext);
  if (!context) {
    throw new Error("useTheme must be used within a ThemeProvider");
  }
  return context;
}
