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
    }
}

export default MediaFileService;