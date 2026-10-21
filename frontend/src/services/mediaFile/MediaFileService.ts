import type { UploadFileResponse } from "@/types";
import { getBlob, post } from "../common/api/ApiService";

const MediaFileService = {
    uploadFile: async function (file: File | null): Promise<UploadFileResponse | undefined> {
        if (!file) return undefined;

        const formData = new FormData();
        formData.append('file', file)

        return post('files', formData);
    }
}

export default MediaFileService;