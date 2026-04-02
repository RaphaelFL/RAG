export type RuntimeEnvironment = {
  apiBaseUrl: string;
  token: string;
  authMode: AuthMode;
  tenantId: string;
  userId: string;
  userRole: UserRole;
};

export type AuthMode = 'jwt' | 'development-headers';

export type UserRole = 'TenantUser' | 'Analyst' | 'TenantAdmin' | 'PlatformAdmin' | 'McpClient';

export type StatusTone = 'neutral' | 'success' | 'warning' | 'danger';
