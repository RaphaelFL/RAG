export type RuntimeEnvironment = {
  apiBaseUrl: string;
  token: string;
  tenantId: string;
  userId: string;
  userRole: UserRole;
};

export type UserRole = 'TenantUser' | 'Analyst' | 'TenantAdmin' | 'PlatformAdmin' | 'McpClient';

export type StatusTone = 'neutral' | 'success' | 'warning' | 'danger';
