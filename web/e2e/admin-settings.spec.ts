import { expect, test, type Page, type Route } from '@playwright/test';

const storageKey = 'rag-console-environment';

function seedEnvironmentValue(overrides?: Record<string, string>) {
  return {
    apiBaseUrl: 'http://localhost:15214',
    authMode: 'development-headers',
    tenantId: '11111111-1111-1111-1111-111111111111',
    userId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    userRole: 'TenantAdmin',
    ...overrides
  };
}

async function seedEnvironment(page: Page, overrides?: Record<string, string>) {
  await page.addInitScript(
    (payload: { key: string; value: Record<string, string> }) => {
      globalThis.sessionStorage.setItem(payload.key, JSON.stringify(payload.value));
    },
    {
      key: storageKey,
      value: seedEnvironmentValue(overrides)
    }
  );
}

async function fulfillRuntimeEnvironment(route: Route) {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({ ok: true })
  });
}

async function fulfillRuntimeSettings(route: Route) {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
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
    })
  });
}

async function fulfillOperationalAudit(route: Route) {
  await route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify({
      entries: [
        {
          entryId: 'entry-1',
          category: 'retrieval',
          status: 'completed',
          title: 'Consulta operacional',
          summary: 'hybrid | topK 2/3',
          detailsJson: '{"diagnostics":true}',
          createdAtUtc: '2026-04-06T12:00:00Z',
          completedAtUtc: '2026-04-06T12:00:02Z'
        }
      ],
      nextCursor: null
    })
  });
}

test('rota administrativa carrega sem erro de runtime', async ({ page }) => {
  const pageErrors: string[] = [];
  page.on('pageerror', (error) => {
    pageErrors.push(error.message);
  });

  await seedEnvironment(page);

  await page.route('**/api/runtime/environment', fulfillRuntimeEnvironment);
  await page.route('**/api/proxy/api/v1/admin/rag-runtime', fulfillRuntimeSettings);
  await page.route('**/api/proxy/api/v1/platform/operational-audit**', fulfillOperationalAudit);

  await page.goto('/configuracoes-de-administrador');

  await expect(page.getByRole('heading', { name: 'Configuracoes de administrador' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Ambiente' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Runtime RAG' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Auditoria operacional' })).toBeVisible();
  await expect(page.getByText('Consulta operacional')).toBeVisible();
  expect(pageErrors).toEqual([]);
});