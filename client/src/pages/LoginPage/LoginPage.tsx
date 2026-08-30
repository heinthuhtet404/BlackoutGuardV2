import { useState, type FormEvent, useEffect } from "react";
import { Navigate, useNavigate } from "react-router-dom";
import {
    Eye,
    EyeOff,
    ShieldCheck,
    Info,
    ArrowRight,
    Mail,
    Lock,
    LogIn,
    Sparkles,
    Clock,
    Bell,
    Activity,
    Zap,
    Gauge,
    Server,
    Users,
    Building,
} from "lucide-react";
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

    // Load saved email if remember me was checked
    useEffect(() => {
        const savedEmail = localStorage.getItem("remember_email");
        if (savedEmail) {
            setEmail(savedEmail);
            setRememberMe(true);
        }
    }, []);

    if (user) {
        return <Navigate to="/overview" replace />;
    }

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();
        setError(null);
        setSubmitting(true);
        try {
            await login(email, password);
            if (rememberMe) {
                localStorage.setItem("remember_email", email);
            } else {
                localStorage.removeItem("remember_email");
            }
            navigate("/overview", { replace: true });
        } catch (err) {
            setError(err instanceof Error ? err.message : "Login failed. Please check your credentials.");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className={styles.page}>
            <div className={styles.container}>
                {/* Left Side - Brand / Hero Section */}
                <div className={styles.heroSection}>
                    <div className={styles.heroContent}>
                        <div className={styles.brandBadge}>
                            <ShieldCheck size={20} />
                            <span>BlackoutGuard</span>
                        </div>
                        <h1 className={styles.heroTitle}>Monitor Your Backup Power</h1>
                        <p className={styles.heroSubtext}>
                            Real-time insights, instant alerts, and complete visibility into
                            your generator performance.
                        </p>
                        <div className={styles.stats}>
                            <div className={styles.stat}>
                                <span className={styles.statValue}>99.9%</span>
                                <span className={styles.statLabel}>Uptime</span>
                            </div>
                            <div className={styles.statDivider}></div>
                            <div className={styles.stat}>
                                <span className={styles.statValue}>24/7</span>
                                <span className={styles.statLabel}>Monitoring</span>
                            </div>
                            <div className={styles.statDivider}></div>
                            <div className={styles.stat}>
                                <span className={styles.statValue}>Instant</span>
                                <span className={styles.statLabel}>Alerts</span>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Right Side - Login Form */}
                <form className={styles.card} onSubmit={(e) => void handleSubmit(e)} noValidate>
                    <div className={styles.cardHeader}>
                        <div className={styles.cardHeaderIconWrapper}>
                            <LogIn size={24} />
                        </div>
                        <div>
                            <h2 className={styles.cardTitle}>Welcome Back</h2>
                            <p className={styles.cardSubtitle}>Sign in to access your dashboard</p>
                        </div>
                    </div>

                    {/* Email Input */}
                    <div className={styles.inputGroup}>
                        <div className={styles.inputIconWrapper}>
                            <Mail size={16} className={styles.inputIcon} />
                            <Input
                                label="Email Address"
                                type="email"
                                placeholder="user@hospital.com"
                                value={email}
                                onChange={(event) => setEmail(event.target.value)}
                                required
                                data-testid="login-email"
                            />
                        </div>
                    </div>

                    {/* Password Input with Show/Hide Toggle */}
                    <div className={styles.inputGroup}>
                        <div className={styles.passwordWrapper}>
                            <div className={styles.inputIconWrapper}>
                                <Lock size={16} className={styles.inputIcon} />
                                <Input
                                    label="Password"
                                    type={showPassword ? "text" : "password"}
                                    placeholder="••••••••••••"
                                    value={password}
                                    onChange={(event) => setPassword(event.target.value)}
                                    required
                                    data-testid="login-password"
                                />
                            </div>
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
                    </div>

                    {/* Error Alert */}
                    {error && (
                        <div className={styles.error} role="alert" data-testid="login-error">
                            <span className={styles.errorIcon}>⚠</span>
                            {error}
                        </div>
                    )}

                    {/* Submit Button */}
                    <Button type="submit" disabled={submitting} data-testid="login-submit" className={styles.submitBtn}>
                        {submitting ? (
                            <>
                                <span className={styles.spinner}></span>
                                Signing in...
                            </>
                        ) : (
                            <>
                                Sign In
                                <ArrowRight size={18} className={styles.btnIcon} />
                            </>
                        )}
                    </Button>

                    {/* Footer */}
                    <div className={styles.footer}>
                        <div className={styles.adminNotice}>
                            <Info size={16} />
                            <span>Forgot password? Contact your administrator.</span>
                        </div>

                        <div className={styles.divider}>
                            <span className={styles.dividerLine}></span>
                            <span className={styles.dividerText}>or</span>
                            <span className={styles.dividerLine}></span>
                        </div>

                        <button
                            type="button"
                            className={styles.registerButton}
                            onClick={() => navigate("/register")}
                        >
                            <Building size={16} />
                            Create New Organization
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}