import type { UploadFileResponse } from "@/types";
import { getBlob, post } from "../common/api/ApiService";

const MediaFileService = {
    uploadFile: async function (file: File | null): Promise<UploadFileResponse | undefined> {
        if (!file) return undefined;

        const formData = new FormData();
        formData.append('file', file)

        return post('file', formData);
    },

    getBlob: async function (id: string): Promise<Blob> {
        return getBlob(`file/${id}`);
    },

    blobToBase64: async function (blob: Blob): Promise<string | undefined> {
        const reader = new FileReader();

        await new Promise((resolve, reject) => {
            reader.onload = resolve;
            reader.onerror = reject;
            reader.readAsDataURL(blob);
        });

        return reader.result?.toString();
    }
}

export default MediaFileService;