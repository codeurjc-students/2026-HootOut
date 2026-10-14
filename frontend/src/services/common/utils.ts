import type { ValidationError } from "@/types";

export function newId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
        return crypto.randomUUID();
    }
    // fallback for insecure contexts
    return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

export function getValidationErrors(err: ValidationError): Map<string, Map<string, string>> {
    const result = new Map<string, Map<string, string>>();
    err.errors.forEach(x => {
        if (!result.has(x.propertyName!)) {
            result.set(x.propertyName!, new Map<string, string>())
        }
        const entry = result.get(x.propertyName!);
        entry?.set(x.errorCode!, x.errorMessage!)
    });
    return result;
}
