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
        buffer += decoder.decode();
      } else if (value) {
        buffer += decoder.decode(value, { stream: true });
      }

      const { segments, rest } = extractSegments(buffer);
      buffer = rest;

      for (const segment of segments) {
        const parsedEvent = parseSegment(segment);
        if (parsedEvent) {
          yield parsedEvent;
        }
      }

      if (done) {
        const parsedEvent = parseSegment(buffer);
        if (parsedEvent) {
          yield parsedEvent;
        }

        break;
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function extractSegments(buffer: string): { segments: string[]; rest: string } {
  const separatorRegex = /\r?\n\r?\n/g;
  const segments: string[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = separatorRegex.exec(buffer)) !== null) {
    segments.push(buffer.slice(lastIndex, match.index));
    lastIndex = match.index + match[0].length;
  }

  return {
    segments,
    rest: buffer.slice(lastIndex)
  };
}

function parseSegment(segment: string): ServerEvent | null {
  const normalized = segment.replaceAll('\r', '').trim();
  if (!normalized) {
    return null;
  }

  const lines = normalized.split('\n');
  const event = lines.find((line) => line.startsWith('event:'))?.slice(6).trim() ?? 'message';
  const data = lines
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice(5).trim())
    .join('\n');

  return data ? { event, data } : null;
}