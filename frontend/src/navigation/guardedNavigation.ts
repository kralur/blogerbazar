const guardedNavigationEvent = "bloggerbazar:guarded-navigation";

export type GuardedNavigationDetail = { destination: string };

export function requestGuardedNavigation(destination: string) {
  const event = new CustomEvent<GuardedNavigationDetail>(guardedNavigationEvent, {
    cancelable: true,
    detail: { destination }
  });
  window.dispatchEvent(event);
  return event.defaultPrevented;
}

export { guardedNavigationEvent };
