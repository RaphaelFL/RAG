import { expect, test, type Page, type Route } from '@playwright/test';
import type { RagRuntimeSettings } from '@/features/chat/types/chat';

const storageKey = 'rag-console-environment';
const apiBaseUrl = 'http://localhost:5000';
const appOrigin = 'http://127.0.0.1:3001';
const proxyApiBasePath = '**/api/proxy/api/v1';

const defaultRuntimeSettings: RagRuntimeSettings = {
  denseChunkSize: 1200,
  denseOverlap: 120,
  narrativeChunkSize: 1800,
  narrativeOverlap: 180,
  minimumChunkCharacters: 120,
  retrievalCandidateMultiplier: 3,
  retrievalMaxCandidateCount: 12,
  maxContextChunks: 6,
  minimumRerankScore: 0.4,
  exactMatchBoost: 0.25,
  titleMatchBoost: 0.2,
  filterMatchBoost: 0.15,
  retrievalCacheTtlSeconds: 90,
  chatCompletionCacheTtlSeconds: 45,
  embeddingCacheTtlHours: 12
};

function corsHeaders(headers?: Record<string, string>) {
  return {
    'Access-Control-Allow-Origin': appOrigin,
    'Access-Control-Allow-Credentials': 'true',
    'Access-Control-Allow-Headers': 'authorization, content-type, x-tenant-id, x-user-id, x-user-role',
    'Access-Control-Allow-Methods': 'GET, POST, PUT, OPTIONS',
    ...headers
  };
}

async function fulfillCorsPreflight(route: Route) {
  await route.fulfill({
    status: 204,
    headers: corsHeaders()
  });
}

async function mockRuntimeRag(page: Page) {
  await page.route(`${proxyApiBasePath}/admin/rag-runtime`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await fulfillCorsPreflight(route);
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: corsHeaders(),
      body: JSON.stringify(defaultRuntimeSettings)
    });
  });
}

type SeedEnvironmentValue = {
  apiBaseUrl: string;
  authMode: string;
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
        apiBaseUrl,
        authMode: 'development-headers',
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

async function sendQuestion(page: import('@playwright/test').Page, question: string) {
  await page.getByRole('textbox', { name: 'Pergunta' }).fill(question);
  await page.getByRole('button', { name: 'Enviar pergunta' }).click();
}

test('E2E-001 usuario autenticado envia pergunta grounded e recebe resposta com citations', async ({ page }) => {
  await seedEnvironment(page);
  await mockRuntimeRag(page);

  await page.route(`${proxyApiBasePath}/chat/stream`, async (route) => {
    const request = route.request();

    if (request.method() === 'OPTIONS') {
      await fulfillCorsPreflight(route);
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'text/event-stream',
      headers: corsHeaders({
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive'
      }),
      body: [
        'event: started\n' +
          'data: {"data":{"answerId":"answer-1","sessionId":"session-1"}}\n\n',
        'event: delta\n' +
          'data: {"data":{"text":"A politica de reembolso exige aprovacao do gestor acima de R$ 500."}}\n\n',
        'event: citation\n' +
          'data: {"data":{"documentId":"doc-1","documentTitle":"Politica de Reembolso","chunkId":"chunk-7","snippet":"Solicitacoes acima de R$ 500 exigem aprovacao do gestor.","score":0.98,"location":{"page":3,"section":"Aprovacao"}}}\n\n',
        'event: completed\n' +
          'data: {"data":{"usage":{"promptTokens":120,"completionTokens":42,"totalTokens":162,"latencyMs":240,"model":"gpt-4.1-mini","retrievalStrategy":"hybrid"}}}\n\n'
      ].join('')
    });
  });

  await openConsole(page);
  await sendQuestion(page, 'Qual e a regra para reembolso acima de R$ 500?');

  await expect(page.getByText('A politica de reembolso exige aprovacao do gestor acima de R$ 500.')).toBeVisible();
  await expect(page.locator('.citation-card').getByText('Politica de Reembolso', { exact: true })).toBeVisible();
  await expect(page.locator('.citation-card').getByText('Solicitacoes acima de R$ 500 exigem aprovacao do gestor.')).toBeVisible();
  await expect(page.getByText('hybrid')).toBeVisible();
});

test('E2E-003 streaming SSE renderiza eventos em ordem e encerra limpo', async ({ page }) => {
  await seedEnvironment(page);
  await mockRuntimeRag(page);

  await page.route(`${proxyApiBasePath}/chat/stream`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await fulfillCorsPreflight(route);
      return;
    }

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
      headers: corsHeaders({
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive'
      }),
      body
    });
  });

  await openConsole(page);
  await sendQuestion(page, 'Mostre a resposta via streaming.');

  await expect(page.getByText('Resposta em streaming')).toBeVisible();
  await expect(page.locator('.citation-card').getByText('Manual Operacional', { exact: true })).toBeVisible();
  await expect(page.getByText('Streaming ocioso')).toBeVisible();
  await expect(page.getByText('62 tokens')).toBeVisible();
});

test('E2E-002 usuario sem permissao recebe bloqueio ao tentar acessar documento alheio', async ({ page }) => {
  await seedEnvironment(page, { userRole: 'TenantAdmin' });
  await mockRuntimeRag(page);

  await page.route(`${proxyApiBasePath}/documents/ingest`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await fulfillCorsPreflight(route);
      return;
    }

    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      headers: corsHeaders(),
      body: JSON.stringify({
        documentId: 'doc-foreign',
        status: 'Queued',
        ingestionJobId: 'job-403',
        timestampUtc: '2026-03-31T22:05:00Z',
        createdAtUtc: '2026-03-31T22:05:00Z'
      })
    });
  });

  await page.route(`${proxyApiBasePath}/documents/suggest-metadata`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await fulfillCorsPreflight(route);
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: corsHeaders(),
      body: JSON.stringify({
        suggestedTitle: 'Documento privado',
        suggestedCategory: 'financeiro',
        suggestedCategories: ['financeiro'],
        suggestedTags: ['sigiloso'],
        strategy: 'heuristic-direct',
        previewText: 'Conteudo analisado automaticamente.'
      })
    });
  });

  await page.route(`${proxyApiBasePath}/documents/doc-foreign`, async (route) => {
    if (route.request().method() === 'OPTIONS') {
      await fulfillCorsPreflight(route);
      return;
    }

    await route.fulfill({
      status: 403,
      contentType: 'application/json',
      headers: corsHeaders(),
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
  await expect(uploadForm.getByText('Sugestao automatica pronta para revisao.')).toBeVisible();
  await expect(page.getByLabel('Titulo logico')).toHaveValue('Documento privado');
  await expect(page.getByLabel('Categoria')).toHaveValue('financeiro');
  await expect(page.getByLabel('Tags')).toHaveValue('sigiloso');
  await page.getByRole('button', { name: 'Confirmar envio' }).click();

  await expect(page.locator('.error-banner').getByText('Acesso negado ao documento solicitado.', { exact: true })).toBeVisible();
  await expect(page.locator('.upload-status-entry .badge').getByText('Failed', { exact: true })).toBeVisible();
  await expect(page.locator('.upload-status-entry').getByText('Documento privado', { exact: true })).toBeVisible();
});

for (const status of [401, 429]) {
  test(`E2E-00${status === 401 ? '4' : '5'} resposta ${status} exibe erro padronizado no chat`, async ({ page }) => {
    await seedEnvironment(page);
    await mockRuntimeRag(page);

    await page.route(`${proxyApiBasePath}/chat/stream`, async (route) => {
      if (route.request().method() === 'OPTIONS') {
        await fulfillCorsPreflight(route);
        return;
      }

      await route.fulfill({
        status,
        contentType: 'application/json',
        headers: corsHeaders(),
        body: JSON.stringify({
          code: status === 401 ? 'unauthorized' : 'rate_limit',
          message: status === 401 ? 'Nao autenticado.' : 'Limite excedido, tente novamente.'
        })
      });
    });

    await openConsole(page);
    await sendQuestion(page, `Teste de erro ${status}`);

    await expect(
      page.locator('.error-banner').getByText(
        status === 401 ? 'Nao autenticado.' : 'Limite excedido, tente novamente.',
        { exact: true }
      )
    ).toBeVisible();
  });
}