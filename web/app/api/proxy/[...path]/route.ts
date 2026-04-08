import { NextResponse } from 'next/server';
import { buildBackendUrl, buildForwardedAuthHeaders, normalizeRuntimeEnvironment } from '@/lib/runtimeEnvironment';
import { readRuntimeEnvironmentCookie } from '@/lib/server/runtimeEnvironmentCookie';

export const runtime = 'nodejs';
export const dynamic = 'force-dynamic';

type RouteContext = {
  params: Promise<{
    path?: string[];
  }>;
};

export async function GET(request: Request, context: RouteContext) {
  return handleProxyRequest(request, context);
}

export async function HEAD(request: Request, context: RouteContext) {
  return handleProxyRequest(request, context);
}

export async function POST(request: Request, context: RouteContext) {
  return handleProxyRequest(request, context);
}

export async function PUT(request: Request, context: RouteContext) {
  return handleProxyRequest(request, context);
}

export async function PATCH(request: Request, context: RouteContext) {
  return handleProxyRequest(request, context);
}

export async function DELETE(request: Request, context: RouteContext) {
  return handleProxyRequest(request, context);
}

async function handleProxyRequest(request: Request, context: RouteContext) {
  const { path = [] } = await context.params;
  const nextUrl = new URL(request.url);
  const cookieValue = request.headers.get('cookie');
  const runtimeEnvironment = resolveRuntimeEnvironment(cookieValue);
  const backendUrl = buildBackendUrl(runtimeEnvironment.apiBaseUrl, `/${path.join('/')}`, nextUrl.search);

  try {
    const upstream = await fetch(backendUrl, {
      method: request.method,
      headers: buildUpstreamHeaders(request, runtimeEnvironment),
      body: await readProxyBody(request),
      cache: 'no-store',
      redirect: 'manual'
    });

    return createProxyResponse(upstream);
  } catch (error) {
    return NextResponse.json(
      {
        message: 'Falha ao encaminhar a requisicao para o backend.',
        backendUrl,
        details: error instanceof Error ? error.message : 'Erro de rede desconhecido.'
      },
      {
        status: 503
      }
    );
  }
}

function resolveRuntimeEnvironment(cookieHeader: string | null) {
  const cookieMap = new Map<string, string>();
  for (const entry of (cookieHeader ?? '').split(';')) {
    const [rawName, ...rawValue] = entry.split('=');
    const name = rawName?.trim();
    if (!name || rawValue.length === 0) {
      continue;
    }

    cookieMap.set(name, rawValue.join('=').trim());
  }

  return readRuntimeEnvironmentCookie(cookieMap.get('rag-runtime-environment')) ?? normalizeRuntimeEnvironment(null);
}

function buildUpstreamHeaders(request: Request, environment: ReturnType<typeof normalizeRuntimeEnvironment>) {
  const headers = new Headers();
  const requestHeaders = request.headers;
  const contentType = requestHeaders.get('content-type');
  const accept = requestHeaders.get('accept');
  const range = requestHeaders.get('range');

  if (contentType) {
    headers.set('content-type', contentType);
  }

  if (accept) {
    headers.set('accept', accept);
  }

  if (range) {
    headers.set('range', range);
  }

  const forwardedAuthHeaders = buildForwardedAuthHeaders(environment);
  forwardedAuthHeaders.forEach((value, key) => {
    headers.set(key, value);
  });

  return headers;
}

async function readProxyBody(request: Request) {
  if (request.method === 'GET' || request.method === 'HEAD') {
    return undefined;
  }

  return await request.arrayBuffer();
}

function createProxyResponse(upstream: Response) {
  const responseHeaders = new Headers();
  const contentType = upstream.headers.get('content-type');
  const cacheControl = upstream.headers.get('cache-control');
  const location = upstream.headers.get('location');
  const etag = upstream.headers.get('etag');
  const contentDisposition = upstream.headers.get('content-disposition');
  const contentLength = upstream.headers.get('content-length');
  const contentRange = upstream.headers.get('content-range');
  const acceptRanges = upstream.headers.get('accept-ranges');
  const lastModified = upstream.headers.get('last-modified');

  if (contentType) {
    responseHeaders.set('content-type', contentType);
  }

  if (cacheControl) {
    responseHeaders.set('cache-control', cacheControl);
  }

  if (location) {
    responseHeaders.set('location', location);
  }

  if (etag) {
    responseHeaders.set('etag', etag);
  }

  if (contentDisposition) {
    responseHeaders.set('content-disposition', contentDisposition);
  }

  if (contentLength) {
    responseHeaders.set('content-length', contentLength);
  }

  if (contentRange) {
    responseHeaders.set('content-range', contentRange);
  }

  if (acceptRanges) {
    responseHeaders.set('accept-ranges', acceptRanges);
  }

  if (lastModified) {
    responseHeaders.set('last-modified', lastModified);
  }

  if (contentType?.includes('text/event-stream')) {
    responseHeaders.set('cache-control', 'no-cache, no-transform');
    responseHeaders.set('connection', 'keep-alive');
  }

  return new Response(upstream.body, {
    status: upstream.status,
    statusText: upstream.statusText,
    headers: responseHeaders
  });
}