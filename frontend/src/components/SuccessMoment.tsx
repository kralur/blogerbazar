import { useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useTelegram } from "../telegram/TelegramProvider";

export const successMomentDurationMs = 1400;

// A short "done" moment for the actions that matter (application sent, offer accepted, deal completed, review
// published): a drawn check with the message and one success buzz, gone by itself. A new message (or a new key)
// starts it again. Reduced motion shows the finished check without the drawing.
export function SuccessMoment({ message, onDone }: { message: string; onDone?: () => void }) {
  const { haptic } = useTelegram();
  const [visible, setVisible] = useState(Boolean(message));
  const onDoneRef = useRef(onDone);
  onDoneRef.current = onDone;

  useEffect(() => {
    if (!message) {
      setVisible(false);
      return;
    }
    setVisible(true);
    haptic.success();
    const timer = window.setTimeout(() => {
      setVisible(false);
      onDoneRef.current?.();
    }, successMomentDurationMs);
    return () => window.clearTimeout(timer);
  }, [haptic, message]);

  if (!message || !visible || typeof document === "undefined") return null;
  return createPortal(
    <div aria-live="polite" className="success-moment" role="status">
      <div className="success-moment__card">
        <svg aria-hidden="true" className="success-moment__mark" viewBox="0 0 64 64">
          <circle className="success-moment__ring" cx="32" cy="32" r="29" />
          <path className="success-moment__check" d="M19 33.5 28 42l17-19" />
        </svg>
        <p>{message}</p>
      </div>
    </div>,
    document.body
  );
}
