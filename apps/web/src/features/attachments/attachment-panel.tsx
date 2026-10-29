"use client";
import { useId, useRef, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Download, Paperclip, LockKeyhole } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { User } from "@/features/auth/auth-provider";
import { ApiError } from "@/lib/api";
import { useErrorMessage } from "@/lib/use-error-message";
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
  const locale = useLocale();
  const t = useTranslations("Attachments");
  const errorMessage = useErrorMessage();
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
      setError(t("chooseFirst"));
      return;
    }
    if (!selected.size || selected.size > 10485760) {
      setError(t("sizeError"));
      return;
    }
    if (!/\.(pdf|png|jpe?g|txt)$/i.test(selected.name)) {
      setError(t("typeError"));
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
      setNotice(t("uploaded"));
    } catch (failure) {
      if (!isCurrent()) return;
      if (failure instanceof ApiError && failure.status === 409) {
        refresh();
        setError(errorMessage(failure) + " " + t("conflict"));
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
      aria-label={messageId ? t("replyAria") : t("ticketAria")}
    >
      <h3>
        <Paperclip size={14} />
        {messageId ? t("reply") : t("ticket")}
        {internal && <LockKeyhole size={12} aria-label={t("teamOnly")} />}
      </h3>
      <p className="field-hint">
        {internal ? t("internalHint") : t("publicHint")}
      </p>
      {query.isPending ? (
        <p role="status">{t("loading")}</p>
      ) : query.error ? (
        <div role="alert" className="error-box">
          <p>
            {query.error instanceof ApiError && query.error.status === 404
              ? t("notAvailable")
              : t("loadError") + " " + errorMessage(query.error)}
          </p>
          <Button
            size="small"
            variant="outline"
            onClick={() => void query.refetch()}
          >
            {t("retry")}
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
                    ? `${new Intl.NumberFormat(locale).format(Number(file.size))} ${t("bytes")}`
                    : Number(file.size) < 1048576
                      ? `${(Number(file.size) / 1024).toFixed(1)} KiB`
                      : `${(Number(file.size) / 1048576).toFixed(1)} MiB`}{" "}
                  · {file.contentType}
                </small>
              </span>
              <Button
                size="small"
                variant="outline"
                aria-label={t("download") + " " + file.fileName}
                disabled={downloading !== null}
                onClick={() => void download(file.id)}
              >
                <Download size={12} />
                {downloading === file.id ? t("saving") : t("download")}
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        <p className="field-hint">{t("empty")}</p>
      )}
      <form
        onSubmit={(event) => {
          event.preventDefault();
          void upload();
        }}
        className="attachment-upload"
      >
        <label htmlFor={id}>
          {messageId ? t("chooseReply") : t("chooseTicket")}
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
          {t("limits")}
        </p>
        {selected && (
          <p className="attachment-selection">
            {t("selected")} {selected.name}
          </p>
        )}
        {closed && <p className="field-hint">{t("closedHint")}</p>}
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
          {uploading ? t("uploading") : t("upload")}
        </Button>
      </form>
    </section>
  );
}
