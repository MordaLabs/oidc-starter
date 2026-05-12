export type BffCurrentUser = {
  isAuthenticated: boolean;
  sub?: string | null;
  name?: string | null;
  username?: string | null;
  email?: string | null;
  roles?: readonly string[];
};
