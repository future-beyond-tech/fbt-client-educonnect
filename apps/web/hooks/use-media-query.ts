import { useEffect, useState } from "react";

export function useMediaQuery(query: string): boolean {
  const [matches, setMatches] = useState<boolean>(false);

  useEffect(() => {
    const media = window.matchMedia(query);

    if (media.matches !== matches) {
      setMatches(media.matches);
    }

    const listener = (): void => {
      setMatches(media.matches);
    };

    media.addEventListener("change", listener);

    return (): void => {
      media.removeEventListener("change", listener);
    };
  }, [matches, query]);

  return matches;
}
