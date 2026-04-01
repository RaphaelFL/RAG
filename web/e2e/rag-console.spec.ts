import { expect, test } from '@playwright/test';

const storageKey = 'rag-console-environment';
type SeedEnvironmentValue = {
  apiBaseUrl: string;
  tenantId: string;
  userId: string;
  userRole: string;
};

async function seedEnvironment(page: import('@playwright/test').Page, overrides?: Record<string, string>) {
  await page.addInitScript(
    (payload: { key: string; value: SeedEnvironmentValue & Record<string, string> }) => {
      globalThis.sessionStorage.setItem(payload.key, JSON.stringify(payload.value));
    },
    {
      key: storageKey,
      value: {
        apiBaseUrl: 'http://localhost:5000',
        tenantId: '11111111-1111-1111-1111-111111111111',
        userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        userRole: 'TenantAdmin',
        ...overrides
      }
    }
  );
}

async function openConsole(page: import('@playwright/test').Page) {
  await page.goto('/');
}

async function sendQuestion(page: import('@playwright/test').Page, question: string, streaming: boolean) {
  const streamingToggle = page.getByRole('checkbox', { name: 'Streaming SSE' });
  if (streaming) {
    await streamingToggle.check();
  } else {
    await streamingToggle.uncheck();
  }

  await page.getByRole('textbox', { name: 'Pergunta' }).fill(question);
  await page.getByRole('button', { name: 'Enviar pergunta' }).click();
}

test('E2E-001 usuario autenticado envia pergunta grounded e recebe resposta com citations', async ({ page }) => {
  await seedEnvironment(page);

  await page.route('http://localhost:5000/api/v1/chat/message', async (route) => {
    const request = route.request();

    await expect(request.headerValue('x-tenant-id')).resolves.toBe('11111111-1111-1111-1111-111111111111');
    await expect(request.headerValue('x-user-role')).resolves.toBe('TenantAdmin');

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        answerId: 'answer-1',
        sessionId: 'session-1',
        message: 'A politica de reembolso exige aprovacao do gestor acima de R$ 500.',
        citations: [
          {
            documentId: 'doc-1',
            documentTitle: 'Politica de Reembolso',
            chunkId: 'chunk-7',
            snippet: 'Solicitacoes acima de R$ 500 exigem aprovacao do gestor.',
            score: 0.98,
            location: {
              page: 3,
              section: 'Aprovacao'
            }
          }
        ],
        usage: {
          promptTokens: 120,
          completionTokens: 42,
          totalTokens: 162,
          latencyMs: 240,
          model: 'gpt-4.1-mini',
          retrievalStrategy: 'hybrid'
        },
        timestampUtc: '2026-03-31T22:00:00Z'
      })
    });
  });

  await openConsole(page);
  await sendQuestion(page, 'Qual e a regra para reembolso acima de R$ 500?', false);

  await expect(page.getByText('A politica de reembolso exige aprovacao do gestor acima de R$ 500.')).toBeVisible();
  await expect(page.locator('.citation-card').getByText('Politica de Reembolso', { exact: true })).toBeVisible();
  await expect(page.locator('.citation-card').getByText('Solicitacoes acima de R$ 500 exigem aprovacao do gestor.')).toBeVisible();
  await expect(page.getByText('hybrid')).toBeVisible();
});

test('E2E-003 streaming SSE renderiza eventos em ordem e encerra limpo', async ({ page }) => {
  await seedEnvironment(page);

  await page.route('http://localhost:5000/api/v1/chat/stream', async (route) => {
    const body = [
      'event: started\n' +
        'data: {"data":{"answerId":"answer-stream","sessionId":"session-stream"}}\n\n',
      'event: delta\n' +
        'data: {"data":{"text":"Resposta "}}\n\n',
      'event: delta\n' +
        'data: {"data":{"text":"em streaming"}}\n\n',
      'event: citation\n' +
        'data: {"data":{"documentId":"doc-2","documentTitle":"Manual Operacional","chunkId":"chunk-2","snippet":"Trecho de apoio","score":0.91,"location":{"page":2}}}\n\n',
      'event: completed\n' +
        'data: {"data":{"usage":{"promptTokens":50,"completionTokens":12,"totalTokens":62,"latencyMs":180,"model":"gpt-4.1-mini","retrievalStrategy":"hybrid"}}}\n\n'
    ].join('');

    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      headers: {
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive'
      },
      body
    });
  });

  await openConsole(page);
  await sendQuestion(page, 'Mostre a resposta via streaming.', true);

  await expect(page.getByText('Resposta em streaming')).toBeVisible();
  await expect(page.locator('.citation-card').getByText('Manual Operacional', { exact: true })).toBeVisible();
  await expect(page.getByText('Streaming ocioso')).toBeVisible();
  await expect(page.getByText('62 tokens')).toBeVisible();
});

test('E2E-002 usuario sem permissao recebe bloqueio ao tentar acessar documento alheio', async ({ page }) => {
  await seedEnvironment(page, { userRole: 'TenantAdmin' });

  await page.route('http://localhost:5000/api/v1/documents/ingest', async (route) => {
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      body: JSON.stringify({
        documentId: 'doc-foreign',
        status: 'Queued',
        ingestionJobId: 'job-403',
        timestampUtc: '2026-03-31T22:05:00Z',
        createdAtUtc: '2026-03-31T22:05:00Z'
      })
    });
  });

  await page.route('http://localhost:5000/api/v1/documents/doc-foreign', async (route) => {
    await route.fulfill({
      status: 403,
      contentType: 'application/json',
      body: JSON.stringify({
        code: 'document_forbidden',
        message: 'Acesso negado ao documento solicitado.'
      })
    });
  });

  await openConsole(page);

  const uploadForm = page.locator('.upload-form');

  await page.getByLabel('Arquivo').setInputFiles({
    name: 'privado.pdf',
    mimeType: 'application/pdf',
    buffer: Buffer.from('conteudo')
  });
  await uploadForm.getByRole('textbox', { name: 'Titulo logico' }).fill('Documento privado');
  await uploadForm.getByRole('textbox', { name: 'Categoria' }).fill('financeiro');
  await uploadForm.getByRole('textbox', { name: 'Tags' }).fill('sigiloso');
  await page.getByRole('button', { name: 'Enviar documento' }).click();

  await expect(page.locator('.error-banner').getByText('Acesso negado ao documento solicitado.', { exact: true })).toBeVisible();
  await expect(page.getByText(/status: failed/i)).toBeVisible();
  await expect(page.getByText('privado.pdf')).toBeVisible();
});

for (const status of [401, 429]) {
  test(`E2E-00${status === 401 ? '4' : '5'} resposta ${status} exibe erro padronizado no chat`, async ({ page }) => {
    await seedEnvironment(page);

    await page.route('http://localhost:5000/api/v1/chat/message', async (route) => {
      await route.fulfill({
        status,
        contentType: 'application/json',
        body: JSON.stringify({
          code: status === 401 ? 'unauthorized' : 'rate_limit',
          message: status === 401 ? 'Nao autenticado.' : 'Limite excedido, tente novamente.'
        })
      });
    });

    await openConsole(page);
    await sendQuestion(page, `Teste de erro ${status}`, false);

    await expect(
      page.locator('.error-banner').getByText(
        status === 401 ? 'Nao autenticado.' : 'Limite excedido, tente novamente.',
        { exact: true }
      )
    ).toBeVisible();
  });
}