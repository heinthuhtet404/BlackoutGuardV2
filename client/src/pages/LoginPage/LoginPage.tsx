import { useState, type FormEvent } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import { Eye, EyeOff, ShieldCheck, Info } from "lucide-react";
import { useAuth } from "../../auth/authTypes";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import styles from "./LoginPage.module.css";

export function LoginPage() {
    const { login, user } = useAuth();
    const navigate = useNavigate();

    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [showPassword, setShowPassword] = useState(false);
    const [rememberMe, setRememberMe] = useState(false);
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
            // Remember me ရွေးချယ်မှုအလိုက် Session ထိန်းသိမ်းရန်
            if (rememberMe) {
                localStorage.setItem("remember_email", email);
            } else {
                localStorage.removeItem("remember_email");
            }
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
                {/* Header Icon & Title */}
                <div className={styles.header}>
                    <div className={styles.iconBadge}>
                        <ShieldCheck size={32} />
                    </div>
                    <h1 className={styles.title}>BlackoutGuard</h1>
                    <p className={styles.subtitle}>Welcome Back</p>
                </div>

                {/* Email Input */}
                <Input
                    label="Email Address"
                    type="email"
                    placeholder="user@hospital.com"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    required
                    data-testid="login-email"
                />

                {/* Password Input with Show/Hide Toggle */}
                <div className={styles.passwordWrapper}>
                    <Input
                        label="Password"
                        type={showPassword ? "text" : "password"}
                        placeholder="••••••••••••"
                        value={password}
                        onChange={(event) => setPassword(event.target.value)}
                        required
                        data-testid="login-password"
                    />
                    <button
                        type="button"
                        className={styles.eyeButton}
                        onClick={() => setShowPassword(!showPassword)}
                        tabIndex={-1}
                        aria-label={showPassword ? "Hide password" : "Show password"}
                    >
                        {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
                    </button>
                </div>

                {/* Remember Me Option */}
                <div className={styles.rememberRow}>
                    <label className={styles.checkboxLabel}>
                        <input
                            type="checkbox"
                            checked={rememberMe}
                            onChange={(e) => setRememberMe(e.target.checked)}
                            className={styles.checkbox}
                        />
                        <span>Remember me</span>
                    </label>
                </div>

                {/* Error Alert */}
                {error && (
                    <div className={styles.error} role="alert" data-testid="login-error">
                        {error}
                    </div>
                )}

                {/* Submit Button */}
                <Button type="submit" disabled={submitting} data-testid="login-submit">
                    {submitting ? "Signing in..." : "Sign In"}
                </Button>

                {/* Footer Info & Admin Contact Notice */}
                <div className={styles.footer}>
                    <div className={styles.adminNotice}>
                        <Info size={16} />
                        <span>Forgot password? Contact your administrator.</span>
                    </div>

                    <button
                        type="button"
                        className={styles.registerButton}
                        onClick={() => navigate("/register")}
                    >
                        Create New Organization
                    </button>
                </div>
            </form>
        </div>
    );
}