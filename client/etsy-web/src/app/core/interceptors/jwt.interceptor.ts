import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  // Only attach JWT token to internal VDS backend requests, never to 3rd-party public APIs
  const isInternalApi = req.url.includes('5.180.81.148') || 
                        req.url.startsWith('/api') || 
                        req.url.includes('localhost:5263');

  if (isInternalApi) {
    const token = localStorage.getItem('etsy_web_jwt_token');
    if (token) {
      const cloned = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });
      return next(cloned);
    }
  }

  return next(req);
};
