import '@testing-library/jest-dom/vitest';

process.env.NEXT_PUBLIC_API_BASE_URL ??= 'http://localhost:5000';
process.env.NEXT_PUBLIC_DEFAULT_TENANT_ID ??= '11111111-1111-1111-1111-111111111111';
process.env.NEXT_PUBLIC_DEFAULT_USER_ID ??= 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
process.env.NEXT_PUBLIC_DEFAULT_USER_ROLE ??= 'TenantAdmin';
process.env.NEXT_PUBLIC_DEFAULT_TEMPLATE_ID ??= 'grounded_answer';
process.env.NEXT_PUBLIC_DEFAULT_TEMPLATE_VERSION ??= '1.0.0';
process.env.NEXT_PUBLIC_DEFAULT_USE_STREAMING ??= 'true';
process.env.NEXT_PUBLIC_DEFAULT_ALLOW_GENERAL_KNOWLEDGE ??= 'false';
process.env.NEXT_PUBLIC_ALLOWED_CONNECT_ORIGINS ??= 'http://localhost:5000,https://localhost:5001';