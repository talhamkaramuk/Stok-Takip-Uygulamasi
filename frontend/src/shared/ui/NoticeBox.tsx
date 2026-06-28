import type { Notice } from "../types/ui";

export function NoticeBox({ notice }: { notice: Notice }) {
  return <div className={`notice ${notice.type}`}>{notice.message}</div>;
}
