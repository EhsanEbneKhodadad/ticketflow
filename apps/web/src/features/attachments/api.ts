import type { components } from "@ticketflow/api-client";
import { api, apiDownload } from "@/lib/api";
export type Attachment = components["schemas"]["AttachmentView"];
export const attachmentsApi = {
  list: (
    ticketId: string,
    signal: AbortSignal,
    principalId: string,
    isCurrent: () => boolean,
  ) =>
    api<Attachment[]>(`/tickets/${ticketId}/attachments`, {
      signal,
      isCurrent,
      expectedPrincipalId: principalId,
    }),
  upload: (
    ticketId: string,
    file: File,
    version: number,
    messageId: string | undefined,
    principalId: string,
    isCurrent: () => boolean,
  ) => {
    const body = new FormData();
    body.set("file", file);
    body.set("expectedVersion", String(version));
    if (messageId) body.set("messageId", messageId);
    return api<Attachment>(`/tickets/${ticketId}/attachments`, {
      method: "POST",
      body,
      isCurrent,
      expectedPrincipalId: principalId,
    });
  },
  download: (
    ticketId: string,
    id: string,
    principalId: string,
    isCurrent: () => boolean,
  ) =>
    apiDownload(`/tickets/${ticketId}/attachments/${id}/download`, {
      isCurrent,
      expectedPrincipalId: principalId,
    }),
};
