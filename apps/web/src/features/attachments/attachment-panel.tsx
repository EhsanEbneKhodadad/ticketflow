"use client";
import { useId, useRef, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, Paperclip, LockKeyhole } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { User } from "@/features/auth/auth-provider";
import { ApiError, errorMessage } from "@/lib/api";
import { attachmentsApi } from "./api";

type Props = {
  ticketId: string;
  version: number | string;
  closed: boolean;
  principalId: string;
  staff: boolean;
  messageId?: string;
  internal?: boolean;
};
export function AttachmentPanel({
  ticketId,
  version,
  closed,
  principalId,
  staff,
  messageId,
  internal = false,
}: Props) {
  const client = useQueryClient();
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [selected, setSelected] = useState<File | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [uploading, setUploading] = useState(false);
  const [downloading, setDownloading] = useState<string | null>(null);
  const isCurrent = () =>
    client.getQueryData<User>(["session"])?.id === principalId;
  const key = ["attachments", ticketId, principalId];
  const query = useQuery({
    queryKey: key,
    queryFn: ({ signal }) =>
      attachmentsApi.list(ticketId, signal, principalId, isCurrent),
    staleTime: 15000,
  });
  const refresh = () => {
    void client.invalidateQueries({
      queryKey: ["ticket", ticketId, principalId],
    });
    void client.invalidateQueries({ queryKey: key });
    void client.invalidateQueries({ queryKey: ["tickets", principalId] });
    void client.invalidateQueries({ queryKey: ["search", principalId] });
  };
  async function upload() {
    setNotice("");
    setError("");
    if (!selected) {
      setError("Choose a file first.");
      return;
    }
    if (!selected.size || selected.size > 10485760) {
      setError("Choose a nonempty file no larger than 10 MiB.");
      return;
    }
    if (!/\.(pdf|png|jpe?g|txt)$/i.test(selected.name)) {
      setError("Choose a PDF, PNG, JPEG or UTF-8 text (.txt) file.");
      return;
    }
    // Capture the file, principal and version once; a conflict never retries itself.
    const file = selected;
    const expectedVersion = Number(version);
    setUploading(true);
    try {
      await attachmentsApi.upload(
        ticketId,
        file,
        expectedVersion,
        messageId,
        principalId,
        isCurrent,
      );
      if (!isCurrent()) return;
      refresh();
      setSelected(null);
      if (input.current) input.current.value = "";
      setNotice("File uploaded.");
    } catch (failure) {
      if (!isCurrent()) return;
      if (failure instanceof ApiError && failure.status === 409) {
        refresh();
        setError(
          errorMessage(failure) +
            " Your selected file is preserved. Review the updated ticket before uploading again.",
        );
      } else setError(errorMessage(failure));
    } finally {
      if (isCurrent()) setUploading(false);
    }
  }
  async function download(attachmentId: string) {
    setError("");
    setDownloading(attachmentId);
    try {
      const result = await attachmentsApi.download(
        ticketId,
        attachmentId,
        principalId,
        isCurrent,
      );
      if (!isCurrent()) return;
      const url = URL.createObjectURL(result.blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = result.fileName;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      // Allow the browser to begin saving before releasing its local URL.
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (failure) {
      if (isCurrent()) setError(errorMessage(failure));
    } finally {
      if (isCurrent()) setDownloading(null);
    }
  }
  if (internal && !staff) return null;
  const files =
    query.data?.filter(
      (file) =>
        (staff || !file.internal) &&
        (file.messageId ?? undefined) === messageId,
    ) ?? [];
  return (
    <section
      className={"attachment-panel" + (messageId ? " reply-attachments" : "")}
      aria-label={messageId ? "Reply attachments" : "Ticket attachments"}
    >
      <h3>
        <Paperclip size={14} />
        {messageId ? "Reply files" : "Attachments"}
        {internal && <LockKeyhole size={12} aria-label="Team only" />}
      </h3>
      <p className="field-hint">
        {internal
          ? "Team only. These files follow this internal note’s visibility."
          : "Visible to the customer and support team."}
      </p>
      {query.isPending ? (
        <p role="status">Loading files…</p>
      ) : query.error ? (
        <div role="alert" className="error-box">
          <p>
            {query.error instanceof ApiError && query.error.status === 404
              ? "Files are not available to your account."
              : "Files could not be loaded. " + errorMessage(query.error)}
          </p>
          <Button
            size="small"
            variant="outline"
            onClick={() => void query.refetch()}
          >
            Retry files
          </Button>
        </div>
      ) : files.length ? (
        <ul className="attachment-list">
          {files.map((file) => (
            <li key={file.id}>
              <span className="attachment-name">
                {file.fileName}
                <small>
                  {Number(file.size) < 1024
                    ? `${file.size} bytes`
                    : Number(file.size) < 1048576
                      ? `${(Number(file.size) / 1024).toFixed(1)} KiB`
                      : `${(Number(file.size) / 1048576).toFixed(1)} MiB`}{" "}
                  · {file.contentType}
                </small>
              </span>
              <Button
                size="small"
                variant="outline"
                aria-label={"Download " + file.fileName}
                disabled={downloading !== null}
                onClick={() => void download(file.id)}
              >
                <Download size={12} />
                {downloading === file.id ? "Saving…" : "Download"}
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="field-hint">No files attached here yet.</p>
      )}
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void upload();
        }}
        className="attachment-upload"
      >
        <label htmlFor={id}>
          {messageId ? "Choose reply file" : "Choose ticket file"}
        </label>
        <input
          ref={input}
          id={id}
          type="file"
          accept=".pdf,.png,.jpg,.jpeg,.txt"
          disabled={closed || uploading}
          onChange={(event) => {
            setSelected(event.target.files?.[0] ?? null);
            setError("");
            setNotice("");
          }}
          aria-describedby={id + "-limits"}
        />
        <p id={id + "-limits"} className="field-hint">
          PDF, PNG, JPEG or UTF-8 text · 10 MiB per file · Up to 10 files across
          this ticket. File content is checked on upload.
        </p>
        {selected && (
          <p className="attachment-selection">Selected: {selected.name}</p>
        )}
        {closed && (
          <p className="field-hint">
            This ticket is closed. Reopen it to upload files.
          </p>
        )}
        {error && (
          <p role="alert" className="error-box">
            {error}
          </p>
        )}
        {notice && (
          <p role="status" className="inline-success">
            {notice}
          </p>
        )}
        <Button
          size="small"
          variant="outline"
          type="submit"
          disabled={closed || uploading}
        >
          {uploading ? "Uploading…" : "Upload file"}
        </Button>
      </form>
    </section>
  );
}
