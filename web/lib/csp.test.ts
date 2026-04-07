import { buildConnectSrc, buildCsp } from '@/lib/csp';

describe('buildConnectSrc', () => {
  it('mantem o connect-src estrito em producao', () => {
    expect(buildConnectSrc(false)).toBe("'self'");
  });

  it('permite websocket no desenvolvimento sem liberar portas locais extras', () => {
    expect(buildConnectSrc(true)).toBe("'self' ws: wss:");
  });
});

describe('buildCsp', () => {
  it('nao inclui aliases localhost nem portas adicionais na diretiva connect-src', () => {
    const csp = buildCsp('nonce-de-teste', true);

    expect(csp).toContain("connect-src 'self' ws: wss:");
    expect(csp).not.toContain('localhost:15214');
    expect(csp).not.toContain('127.0.0.1');
    expect(csp).not.toContain('localhost:3001');
  });
});