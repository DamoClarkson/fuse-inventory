export const scrumPokerAvatarImages = Array.from(
  { length: 18 },
  (_, index) => ({
    value: `avatar-image-${index + 1}`,
    index,
  }),
);

export const scrumPokerAvatarSheetUrl = "/avatar-sprite.png";

export function scrumPokerAvatarImageStyle(
  avatar: (typeof scrumPokerAvatarImages)[number],
  displaySize = 60,
) {
  const column = avatar.index % 6;
  const row = Math.floor(avatar.index / 6);
  return {
    backgroundImage: `url("${scrumPokerAvatarSheetUrl}")`,
    backgroundPosition: `-${column * displaySize}px -${row * displaySize}px`,
    backgroundSize: `${displaySize * 6}px ${displaySize * 3}px`,
    backgroundRepeat: "no-repeat",
  };
}

export function scrumPokerAvatarForSelection(
  selection: string | null | undefined,
) {
  const image = scrumPokerAvatarImages.find(
    (avatar) => avatar.value === selection,
  );
  return image;
}
