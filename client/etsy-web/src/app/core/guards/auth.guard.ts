import { CanActivateFn } from '@angular/router';

export const authGuard: CanActivateFn = () => {
  return true; // Studio mode: always accessible
};

export const adminGuard: CanActivateFn = () => {
  return true;
};
