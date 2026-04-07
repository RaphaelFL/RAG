export function buildConnectSrc(isDevelopment: boolean) {
  const sources = ["'self'"];

  if (isDevelopment) {
    sources.push('ws:', 'wss:');
  }

  return sources.join(' ');
}

export function buildCsp(nonce: string, isDevelopment: boolean) {
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
    `connect-src ${buildConnectSrc(isDevelopment)}`,
    "frame-ancestors 'none'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'"
  ].join('; ');
}