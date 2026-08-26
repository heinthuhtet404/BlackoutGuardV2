import { useState, type FormEvent } from "react";
import { Navigate, useNavigate, Link } from "react-router-dom";
import { useAuth } from "../../auth/authTypes";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
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
                <div className={styles.heroSection}>
                    <div className={styles.heroContent}>
                        <div className={styles.brandBadge}>⚡ BlackoutGuard</div>
                        <h1 className={styles.heroTitle}>Create Your Account</h1>
                        <p className={styles.heroSubtext}>
                            Start monitoring your backup power systems with real-time insights
                        </p>
                        <div className={styles.features}>
                            <div className={styles.feature}>
                                <span className={styles.featureIcon}>✓</span>
                                <span>Real-time monitoring</span>
                            </div>
                            <div className={styles.feature}>
                                <span className={styles.featureIcon}>✓</span>
                                <span>Instant alerts & notifications</span>
                            </div>
                            <div className={styles.feature}>
                                <span className={styles.featureIcon}>✓</span>
                                <span>Advanced analytics dashboard</span>
                            </div>
                        </div>
                    </div>
                </div>

                <form className={styles.card} onSubmit={(e) => void handleSubmit(e)} noValidate>
                    <div className={styles.cardHeader}>
                        <h2 className={styles.cardTitle}>Get Started</h2>
                        <p className={styles.cardSubtitle}>Fill in your details to register</p>
                    </div>

                    <div className={styles.formGrid}>
                        <div className={styles.formLeft}>
                            <Input
                                label="Full Name *"
                                value={fullName}
                                onChange={(e) => setFullName(e.target.value)}
                                required
                                data-testid="register-fullname"
                                placeholder="John Doe"
                            />

                            <Input
                                label="Email Address *"
                                type="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                required
                                data-testid="register-email"
                                placeholder="john@example.com"
                            />

                            <Input
                                label="Organization Name *"
                                value={organizationName}
                                onChange={(e) => setOrganizationName(e.target.value)}
                                required
                                data-testid="register-organization"
                                placeholder="Acme Corp"
                            />
                        </div>

                        <div className={styles.formRight}>
                            <Input
                                label="Password *"
                                type="password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                required
                                data-testid="register-password"
                                placeholder="••••••••"
                            />

                            <Input
                                label="Confirm Password *"
                                type="password"
                                value={confirmPassword}
                                onChange={(e) => setConfirmPassword(e.target.value)}
                                required
                                data-testid="register-confirm-password"
                                placeholder="••••••••"
                            />

                            <Input
                                label="Generator Capacity (kW) *"
                                type="number"
                                value={generatorCapacity}
                                onChange={(e) => setGeneratorCapacity(e.target.value)}
                                required
                                data-testid="register-capacity"
                                placeholder="e.g., 100"
                            />

                            <Input
                                label="Facility Location (Optional)"
                                value={facilityLocation}
                                onChange={(e) => setFacilityLocation(e.target.value)}
                                data-testid="register-location"
                                placeholder="City, Country"
                            />
                        </div>
                    </div>

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

                    {error && (
                        <div className={styles.error} role="alert" data-testid="register-error">
                            <span className={styles.errorIcon}>⚠</span>
                            {error}
                        </div>
                    )}

                    <Button type="submit" disabled={submitting} data-testid="register-submit" className={styles.submitBtn}>
                        {submitting ? (
                            <>
                                <span className={styles.spinner}></span>
                                Creating Account...
                            </>
                        ) : (
                            "Create Account →"
                        )}
                    </Button>

                    <div className={styles.footer}>
                        Already have an account? <Link to="/login" className={styles.loginLink}>Sign In</Link>
                    </div>
                </form>
            </div>
        </div>
    );
}