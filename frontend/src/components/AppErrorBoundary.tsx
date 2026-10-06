import { Component, type ErrorInfo, type ReactNode } from "react";
import { ErrorState } from "./ui";

// Last-resort guard: a rendering bug shows a retry screen instead of a blank Mini App.
export class AppErrorBoundary extends Component<{ children: ReactNode }, { failed: boolean }> {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error(error, info.componentStack);
  }

  render() {
    if (!this.state.failed) return this.props.children;
    return <div className="screen"><ErrorState onRetry={() => window.location.reload()} /></div>;
  }
}
