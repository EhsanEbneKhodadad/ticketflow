import {
  getApiV1TicketsByIdAttachments,
  getApiV1TicketsByIdAttachmentsByAttachmentIdDownload,
  postApiV1TicketsByIdAttachmentsMutation,
  type AttachmentView,
  type UploadAttachmentForm,
} from "@ticketflow/api-client";
export type Attachment = AttachmentView;
export const attachmentsApi = {
  list: (
    ticketId: string,
    signal: AbortSignal,
    principalId: string,
    isCurrent: () => boolean,
  ) =>
    getApiV1TicketsByIdAttachments({
      path: { id: ticketId },
      signal,
      headers: { "X-TicketFlow-User": principalId },
      throwOnError: true,
    }).then(({ data }) => {
      if (!isCurrent()) throw new Error("The account changed.");
      return data;
    }),
  upload: (
    ticketId: string,
    file: File,
    version: number,
    messageId: string | undefined,
    principalId: string,
    isCurrent: () => boolean,
  ) => {
    const body: UploadAttachmentForm = {
      file,
      expectedVersion: version,
      ...(messageId ? { messageId } : {}),
    };
    return postApiV1TicketsByIdAttachmentsMutation({
      headers: { "X-TicketFlow-User": principalId },
    }).mutationFn!(
      {
        path: { id: ticketId },
        body,
        throwOnError: true,
      },
      undefined as never,
    ).then((data) => {
      if (!isCurrent()) throw new Error("The account changed.");
      return data;
    });
  },
  download: async (
    ticketId: string,
    id: string,
    principalId: string,
    isCurrent: () => boolean,
  ) => {
    const { data, response } =
      await getApiV1TicketsByIdAttachmentsByAttachmentIdDownload({
        path: { id: ticketId, attachmentId: id },
        headers: { "X-TicketFlow-User": principalId },
        throwOnError: true,
      });
    if (!isCurrent()) throw new Error("The account changed.");
    const blob = data instanceof Blob ? data : new Blob([data as BlobPart]);
    const disposition = response.headers.get("Content-Disposition") ?? "";
    const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)?.[1];
    const plain = /filename="([^"\r\n]+)"/i.exec(disposition)?.[1];
    let fileName = plain ?? "attachment";
    if (encoded) {
      try {
        fileName = decodeURIComponent(encoded);
      } catch {
        // Keep the safe fallback.
      }
    }
    return { blob, fileName };
  },
};
