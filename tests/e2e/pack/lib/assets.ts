import { readFile } from 'node:fs/promises';
export async function dataUri(url: URL): Promise<string> {
  return 'data:image/png;base64,' + (await readFile(url)).toString('base64');
}
