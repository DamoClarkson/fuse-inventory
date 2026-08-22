export const scrumPokerAvatarImages = Array.from(
  { length: 18 },
  (_, index) => ({
    value: `avatar-image-${index + 1}`,
    index,
  }),
);

export const scrumPokerAvatarSheetUrl = "/avatar-sprite.png";

const scrumPokerAvatarBackgrounds = [
  "#d8eaf8",
  "#f9d8e5",
  "#f8dfc2",
  "#e8ddf5",
  "#f6edc8",
  "#d8f0df",
];

export const scrumPokerAvatarRequestColors = [
  "#1d4ed8",
  "#2563eb",
  "#3b82f6",
  "#0f766e",
  "#0d9488",
  "#14b8a6",
  "#15803d",
  "#16a34a",
  "#22c55e",
  "#a16207",
  "#ca8a04",
  "#eab308",
  "#c2410c",
  "#ea580c",
  "#f97316",
  "#be123c",
  "#e11d48",
  "#f43f5e",
];

export function scrumPokerAvatarImageStyle(
  avatar: (typeof scrumPokerAvatarImages)[number],
  displaySize = 60,
) {
  const column = avatar.index % 6;
  const row = Math.floor(avatar.index / 6);
  return {
    backgroundImage: `url("${scrumPokerAvatarSheetUrl}")`,
    backgroundColor:
      scrumPokerAvatarBackgrounds[
        avatar.index % scrumPokerAvatarBackgrounds.length
      ],
    backgroundPosition: `-${column * displaySize}px -${row * displaySize}px`,
    backgroundSize: `${displaySize * 6}px ${displaySize * 3}px`,
    backgroundRepeat: "no-repeat",
  };
}

export function scrumPokerAvatarForSelection(
  selection: string | null | undefined,
) {
  const image = scrumPokerAvatarImages.find(
    (avatar) =>
      avatar.value === selection ||
      scrumPokerAvatarRequestColors[avatar.index]?.toLowerCase() ===
        selection?.toLowerCase(),
  );
  return image;
}
