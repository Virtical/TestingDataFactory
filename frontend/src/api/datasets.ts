import { requestResponse } from './request';

export type DatasetFormat = 'csv' | 'json';

export type DatasetDownload = {
    blob: Blob;
    fileName: string;
    seed: string | null;
};

export async function generateDataset(specification: string, format: DatasetFormat, signal: AbortSignal): Promise<DatasetDownload> {
    const response = await requestResponse('/api/datasets/generate', {
        method: 'POST',
        body: { specification, format },
        signal,
    });
    const disposition = response.headers.get('Content-Disposition');

    return {
        blob: await response.blob(),
        fileName: disposition?.match(/filename="([^"]+)"/i)?.[1] ?? `dataset.${format}`,
        seed: response.headers.get('X-Generation-Seed'),
    };
}
