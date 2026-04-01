export type ServerEvent = {
  event: string;
  data: string;
};

export async function* readServerSentEvents(stream: ReadableStream<Uint8Array>): AsyncGenerator<ServerEvent> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const segments = buffer.split('\n\n');
      buffer = segments.pop() ?? '';

      for (const segment of segments) {
        const normalized = segment.replace(/\r/g, '');
        const lines = normalized.split('\n');
        const event = lines.find((line) => line.startsWith('event:'))?.slice(6).trim() ?? 'message';
        const data = lines
          .filter((line) => line.startsWith('data:'))
          .map((line) => line.slice(5).trim())
          .join('\n');

        if (data) {
          yield { event, data };
        }
      }
    }
  } finally {
    reader.releaseLock();
  }
}