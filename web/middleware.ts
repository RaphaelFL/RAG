import type { NextRequest } from 'next/server';
import { NextResponse } from 'next/server';
import { publicRuntimeDefaults } from '@/lib/publicEnv';

const connectSrc = ["'self'", 'https:', ...publicRuntimeDefaults.connectSrcOrigins].join(' ');

export function middleware(request: NextRequest) {
  const nonce = createNonce();
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set('x-csp-nonce', nonce);

  const response = NextResponse.next({
    request: {
      headers: requestHeaders
    }
  });

  const csp = buildCsp(nonce, process.env.NODE_ENV !== 'production');
  response.headers.set('Content-Security-Policy', csp);
  response.headers.set('x-csp-nonce', nonce);
  response.headers.set('Referrer-Policy', 'strict-origin-when-cross-origin');
  response.headers.set('X-Content-Type-Options', 'nosniff');
  response.headers.set('X-Frame-Options', 'DENY');
  response.headers.set('Cross-Origin-Opener-Policy', 'same-origin');
  response.headers.set('Cross-Origin-Resource-Policy', 'same-origin');
  response.headers.set('Permissions-Policy', 'camera=(), microphone=(), geolocation=()');
  return response;
}

function buildCsp(nonce: string, isDevelopment: boolean) {
  const scriptSrc = isDevelopment
    ? `script-src 'self' 'nonce-${nonce}' 'unsafe-eval' 'unsafe-inline'`
    : `script-src 'self' 'nonce-${nonce}'`;
  const styleSrc = isDevelopment
    ? "style-src 'self' 'unsafe-inline'"
    : `style-src 'self' 'nonce-${nonce}'`;

  return [
    "default-src 'self'",
    scriptSrc,
    styleSrc,
    "img-src 'self' data: blob:",
    "font-src 'self' data:",
    `connect-src ${connectSrc}`,
    "frame-ancestors 'none'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'"
  ].join('; ');
}

function createNonce() {
  return btoa(crypto.randomUUID()).replace(/=+$/u, '');
}

export const config = {
  matcher: ['/((?!_next/static|_next/image|favicon.ico).*)']
};