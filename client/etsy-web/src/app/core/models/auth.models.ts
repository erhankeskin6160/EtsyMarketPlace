export interface User {
  id: string;
  username: string;
  email: string;
  role: 'Admin' | 'StoreOwner' | 'Demo';
  assignedShopIds: string[];
  monthlyAiTokenQuota: number;
  usedAiTokens: number;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
}

export interface LoginRequest {
  usernameOrEmail: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  email: string;
  password: string;
  shopId?: string;
}

export interface AuthResponse {
  success: boolean;
  token?: string;
  message?: string;
  user?: User;
}

export interface UpdateUserRequest {
  email: string;
  role: string;
  assignedShopIds: string[];
  monthlyAiTokenQuota: number;
  isActive: boolean;
}

export interface AuditLog {
  id: number;
  userId: string;
  username: string;
  action: string;
  details?: string;
  ipAddress?: string;
  timestamp: string;
}

export interface SystemStats {
  totalUsers: number;
  activeUsers: number;
  totalShops: number;
  totalUsedAiTokens: number;
  auditLogCount: number;
}
