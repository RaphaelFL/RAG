import { describe, expect, it } from 'vitest';

import { readServerSentEvents } from './sse';

describe('readServerSentEvents', () => {
  it('processa eventos com delimitador CRLF', async () => {
    const stream = createStream([
      'event: started\r\n',
      'data: {"data":{"answerId":"answer-1","sessionId":"session-1"}}\r\n\r\n',
      'event: delta\r\n',
      'data: {"data":{"text":"Oi"}}\r\n\r\n'
    ]);

    const events = await collect(stream);

    expect(events).toEqual([
      {
        event: 'started',
        data: '{"data":{"answerId":"answer-1","sessionId":"session-1"}}'
      },
      {
        event: 'delta',
        data: '{"data":{"text":"Oi"}}'
      }
    ]);
  });

  it('mantem suporte a chunks fragmentados', async () => {
    const stream = createStream([
      'event: completed\r',
      '\n',
      'data: {"data":{"usage":{"totalTokens":10}}}',
      '\r\n\r\n'
    ]);

    const events = await collect(stream);

    expect(events).toEqual([
      {
        event: 'completed',
        data: '{"data":{"usage":{"totalTokens":10}}}'
      }
    ]);
  });
});

async function collect(stream: ReadableStream<Uint8Array>) {
  const events = [] as Array<{ event: string; data: string }>;
  for await (const event of readServerSentEvents(stream)) {
    events.push(event);
  }

  return events;
}

function createStream(chunks: string[]) {
  const encoder = new TextEncoder();

  return new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) {
        controller.enqueue(encoder.encode(chunk));
      }

      controller.close();
    }
  });
}