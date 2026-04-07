import type { NextRequest } from 'next/server';
import { NextResponse } from 'next/server';
import { buildCsp } from '@/lib/csp';

export function middleware(request: NextRequest) {
  const nonce = createNonce();
  const isDevelopment = process.env.NODE_ENV !== 'production';
  const requestHeaders = new Headers(request.headers);
  requestHeaders.set('x-csp-nonce', nonce);

  const response = NextResponse.next({
    request: {
      headers: requestHeaders
    }
  });

  const csp = buildCsp(nonce, isDevelopment);
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

function createNonce() {
  return btoa(crypto.randomUUID()).replace(/=+$/u, '');
}

export const config = {
  matcher: ['/((?!_next|favicon.ico).*)']
};