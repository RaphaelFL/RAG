import { NextResponse } from 'next/server';
import { normalizeRuntimeEnvironment } from '@/lib/runtimeEnvironment';
import { createRuntimeEnvironmentCookieOptions, serializeRuntimeEnvironmentCookie } from '@/lib/server/runtimeEnvironmentCookie';

export const runtime = 'nodejs';

export async function PUT(request: Request) {
  const payload = await request.json().catch(() => null);
  const environment = normalizeRuntimeEnvironment(payload);

  const response = NextResponse.json({ ok: true, environment });
  response.cookies.set({
    ...createRuntimeEnvironmentCookieOptions(),
    value: serializeRuntimeEnvironmentCookie(environment)
  });

  return response;
}