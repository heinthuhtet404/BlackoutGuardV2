import { useState, type FormEvent } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/authTypes";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import styles from "./LoginPage.module.css";

export function LoginPage() {
  const { login, user } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (user) {
    return <Navigate to="/overview" replace />;
  }

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login(email, password);
      navigate("/overview", { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed.");
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className={styles.page}>
      <form className={styles.card} onSubmit={(e) => void handleSubmit(e)} noValidate>
        <h1 className={styles.title}>BlackoutGuard</h1>
        <Input
          label="Email"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          required
          data-testid="login-email"
        />
        <Input
          label="Password"
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          required
          data-testid="login-password"
        />
        {error && (
          <div className={styles.error} role="alert" data-testid="login-error">
            {error}
          </div>
        )}
        <Button type="submit" disabled={submitting} data-testid="login-submit">
          {submitting ? "Signing in..." : "Sign In"}
        </Button>
      </form>
    </div>
  );
}
