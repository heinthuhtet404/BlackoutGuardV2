import { useState, type FormEvent } from "react";
import { Navigate, useNavigate, Link } from "react-router-dom";
import { useAuth } from "../../auth/authTypes";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import {
    UserPlus,
    Mail,
    Lock,
    Building,
    MapPin,
    CheckCircle,
    AlertCircle,
    ArrowRight,
    ShieldCheck,
    Zap,
    Bell,
    BarChart3,
    Check,
    User,
    Briefcase,
    Key,
    Shield,
    Sparkles,
    Eye,
    EyeOff,
} from "lucide-react";
import styles from "./RegisterPage.module.css";

export function RegisterPage() {
    const { register, user } = useAuth();
    const navigate = useNavigate();

    const [fullName, setFullName] = useState("");
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [confirmPassword, setConfirmPassword] = useState("");
    const [organizationName, setOrganizationName] = useState("");
    const [generatorCapacity, setGeneratorCapacity] = useState("");
    const [facilityLocation, setFacilityLocation] = useState("");
    const [agreeTerms, setAgreeTerms] = useState(false);

    const [error, setError] = useState<string | null>(null);
    const [submitting, setSubmitting] = useState(false);

    // Password show/hide states
    const [showPassword, setShowPassword] = useState(false);
    const [showConfirmPassword, setShowConfirmPassword] = useState(false);

    if (user) {
        return <Navigate to="/overview" replace />;
    }

    const validatePassword = (pwd: string) => {
        return (
            pwd.length >= 8 &&
            /[A-Z]/.test(pwd) &&
            /[a-z]/.test(pwd) &&
            /[0-9]/.test(pwd) &&
            /[!@#$%^&*(),.?":{}|<>]/.test(pwd)
        );
    };

    const handleSubmit = async (event: FormEvent) => {
        event.preventDefault();
        setError(null);

        if (!validatePassword(password)) {
            setError(
                "Password တွင် အနည်းဆုံး ၈ လုံး၊ စာလုံးကြီး၊ စာလုံးငယ်၊ နံပါတ် နှင့် Special Character ပါဝင်ရပါမည်။"
            );
            return;
        }

        if (password !== confirmPassword) {
            setError("Confirm Password တူညီမှု မရှိပါ။");
            return;
        }

        if (!agreeTerms) {
            setError("Terms of Service ကို သဘောတူရန် လိုအပ်ပါသည်။");
            return;
        }

        setSubmitting(true);
        try {
            await register({
                fullName,
                email,
                password,
                organizationName,
                generatorCapacity: Number(generatorCapacity),
                facilityLocation: facilityLocation || undefined,
            });
            navigate("/login", { replace: true });
        } catch (err) {
            setError(err instanceof Error ? err.message : "Registration failed.");
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div className={styles.page}>
            <div className={styles.container}>
                {/* Left Side - Hero Section */}
                <div className={styles.heroSection}>
                    <div className={styles.heroContent}>
                        <div className={styles.brandBadge}>
                            <ShieldCheck size={16} />
                            <span>BlackoutGuard</span>
                        </div>
                        <h1 className={styles.heroTitle}>Create Your Account</h1>
                        <p className={styles.heroSubtext}>
                            Start monitoring your backup power systems with real-time insights
                        </p>
                        <div className={styles.features}>
                            <div className={styles.feature}>
                                <span className={styles.featureIcon}>
                                    <Zap size={12} />
                                </span>
                                <span>Real-time monitoring</span>
                            </div>
                            <div className={styles.feature}>
                                <span className={styles.featureIcon}>
                                    <Bell size={12} />
                                </span>
                                <span>Instant alerts & notifications</span>
                            </div>
                            <div className={styles.feature}>
                                <span className={styles.featureIcon}>
                                    <BarChart3 size={12} />
                                </span>
                                <span>Analytics dashboard</span>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Right Side - Registration Form */}
                <form className={styles.card} onSubmit={(e) => void handleSubmit(e)} noValidate>
                    <div className={styles.cardHeader}>
                        <div className={styles.cardHeaderIconWrapper}>
                            <UserPlus size={24} />
                        </div>
                        <div>
                            <h2 className={styles.cardTitle}>Get Started</h2>
                            <p className={styles.cardSubtitle}>Fill in your details to register</p>
                        </div>
                    </div>

                    <div className={styles.formGrid}>
                        <div className={styles.formLeft}>
                            {/* Full Name */}
                            <div className={styles.inputIconWrapper}>
                                <User className={styles.inputIcon} size={16} />
                                <div className={styles.inputWrapper}>
                                    <label>Full Name *</label>
                                    <input
                                        type="text"
                                        value={fullName}
                                        onChange={(e) => setFullName(e.target.value)}
                                        required
                                        data-testid="register-fullname"
                                        placeholder="John Doe"
                                    />
                                </div>
                            </div>

                            {/* Email */}
                            <div className={styles.inputIconWrapper}>
                                <Mail className={styles.inputIcon} size={16} />
                                <div className={styles.inputWrapper}>
                                    <label>Email Address *</label>
                                    <input
                                        type="email"
                                        value={email}
                                        onChange={(e) => setEmail(e.target.value)}
                                        required
                                        data-testid="register-email"
                                        placeholder="john@example.com"
                                    />
                                </div>
                            </div>

                            {/* Organization Name */}
                            <div className={styles.inputIconWrapper}>
                                <Briefcase className={styles.inputIcon} size={16} />
                                <div className={styles.inputWrapper}>
                                    <label>Organization Name *</label>
                                    <input
                                        type="text"
                                        value={organizationName}
                                        onChange={(e) => setOrganizationName(e.target.value)}
                                        required
                                        data-testid="register-organization"
                                        placeholder="Acme Corp"
                                    />
                                </div>
                            </div>
                        </div>

                        <div className={styles.formRight}>
                            {/* Password with Toggle */}
                            <div className={styles.inputIconWrapper}>
                                <Key className={styles.inputIcon} size={16} />
                                <div className={styles.inputWrapper}>
                                    <label>Password *</label>
                                    <div className={styles.passwordWrapper}>
                                        <input
                                            type={showPassword ? "text" : "password"}
                                            value={password}
                                            onChange={(e) => setPassword(e.target.value)}
                                            required
                                            data-testid="register-password"
                                            placeholder="••••••••"
                                            className={styles.passwordInput}
                                        />
                                        <button
                                            type="button"
                                            className={styles.toggleButton}
                                            onClick={() => setShowPassword(!showPassword)}
                                            tabIndex={-1}
                                            aria-label={showPassword ? "Hide password" : "Show password"}
                                        >
                                            {showPassword ? (
                                                <EyeOff size={18} className={styles.toggleIcon} />
                                            ) : (
                                                <Eye size={18} className={styles.toggleIcon} />
                                            )}
                                        </button>
                                    </div>
                                </div>
                            </div>

                            {/* Confirm Password with Toggle */}
                            <div className={styles.inputIconWrapper}>
                                <Lock className={styles.inputIcon} size={16} />
                                <div className={styles.inputWrapper}>
                                    <label>Confirm Password *</label>
                                    <div className={styles.passwordWrapper}>
                                        <input
                                            type={showConfirmPassword ? "text" : "password"}
                                            value={confirmPassword}
                                            onChange={(e) => setConfirmPassword(e.target.value)}
                                            required
                                            data-testid="register-confirm-password"
                                            placeholder="••••••••"
                                            className={styles.passwordInput}
                                        />
                                        <button
                                            type="button"
                                            className={styles.toggleButton}
                                            onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                                            tabIndex={-1}
                                            aria-label={showConfirmPassword ? "Hide password" : "Show password"}
                                        >
                                            {showConfirmPassword ? (
                                                <EyeOff size={18} className={styles.toggleIcon} />
                                            ) : (
                                                <Eye size={18} className={styles.toggleIcon} />
                                            )}
                                        </button>
                                    </div>
                                </div>
                            </div>

                            {/* Facility Location */}
                            <div className={styles.inputIconWrapper}>
                                <MapPin className={styles.inputIcon} size={16} />
                                <div className={styles.inputWrapper}>
                                    <label>Facility Location (Optional)</label>
                                    <input
                                        type="text"
                                        value={facilityLocation}
                                        onChange={(e) => setFacilityLocation(e.target.value)}
                                        data-testid="register-location"
                                        placeholder="City, Country"
                                    />
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Terms */}
                    <div className={styles.termsSection}>
                        <label className={styles.checkboxLabel}>
                            <input
                                type="checkbox"
                                checked={agreeTerms}
                                onChange={(e) => setAgreeTerms(e.target.checked)}
                                required
                                className={styles.checkbox}
                            />
                            <span className={styles.checkboxText}>
                                I agree to the <Link to="/terms" className={styles.termsLink}>Terms of Service</Link> and{" "}
                                <Link to="/privacy" className={styles.termsLink}>Privacy Policy</Link>
                            </span>
                        </label>
                    </div>

                    {/* Error */}
                    {error && (
                        <div className={styles.error} role="alert" data-testid="register-error">
                            <AlertCircle size={18} className={styles.errorIcon} />
                            {error}
                        </div>
                    )}

                    {/* Submit Button */}
                    <Button type="submit" disabled={submitting} data-testid="register-submit" className={styles.submitBtn}>
                        {submitting ? (
                            <>
                                <span className={styles.spinner}></span>
                                Creating Account...
                            </>
                        ) : (
                            <>
                                Create Account
                                <ArrowRight size={18} className={styles.btnIcon} />
                            </>
                        )}
                    </Button>

                    {/* Footer */}
                    <div className={styles.footer}>
                        Already have an account? <Link to="/login" className={styles.loginLink}>Sign In</Link>
                    </div>
                </form>
            </div>
        </div>
    );
}