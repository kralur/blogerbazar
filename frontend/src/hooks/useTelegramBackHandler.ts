import { useEffect, useRef } from "react";
import { useTelegram } from "../telegram/TelegramProvider";

export function useTelegramBackHandler(handler: () => void, enabled = true) {
  const { setBackButtonHandler } = useTelegram();
  const handlerRef = useRef(handler);
  handlerRef.current = handler;

  useEffect(() => {
    if (!enabled) return;
    const stableHandler = () => handlerRef.current();
    setBackButtonHandler(stableHandler);
    return () => setBackButtonHandler();
  }, [enabled, setBackButtonHandler]);
}
