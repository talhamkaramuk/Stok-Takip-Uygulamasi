export function containSidebarWheel(event: WheelEvent) {
  const element = event.currentTarget as HTMLElement | null;

  if (!element) {
    return;
  }

  const maxScrollTop = element.scrollHeight - element.clientHeight;

  event.stopPropagation();

  if (maxScrollTop <= 0) {
    event.preventDefault();
    return;
  }

  const nextScrollTop = element.scrollTop + event.deltaY;
  const isPastTop = event.deltaY < 0 && nextScrollTop <= 0;
  const isPastBottom = event.deltaY > 0 && nextScrollTop >= maxScrollTop;

  if (isPastTop || isPastBottom) {
    event.preventDefault();
    element.scrollTop = isPastTop ? 0 : maxScrollTop;
  }
}