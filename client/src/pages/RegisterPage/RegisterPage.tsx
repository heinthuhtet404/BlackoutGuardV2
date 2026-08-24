import { useState, type FormEvent } from "react";
import { Navigate, useNavigate, Link } from "react-router-dom";
import { useAuth } from "../../auth/authTypes";
import { Button } from "../../components/ui/Button";
import { Input } from "../../components/ui/Input";
import styles from "../LoginPage/LoginPage.module.css";

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
            <form className={styles.card} onSubmit={(e) => void handleSubmit(e)} noValidate>
                <h1 className={styles.title}>BlackoutGuard Register</h1>

                <Input
                    label="Full Name *"
                    value={fullName}
                    onChange={(e) => setFullName(e.target.value)}
                    required
                    data-testid="register-fullname"
                />

                <Input
                    label="Email Address *"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    data-testid="register-email"
                />

                <Input
                    label="Password *"
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    data-testid="register-password"
                />

                <Input
                    label="Confirm Password *"
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                    data-testid="register-confirm-password"
                />

                <Input
                    label="Organization Name *"
                    value={organizationName}
                    onChange={(e) => setOrganizationName(e.target.value)}
                    required
                    data-testid="register-organization"
                />

                <Input
                    label="Generator Capacity (kW) *"
                    type="number"
                    value={generatorCapacity}
                    onChange={(e) => setGeneratorCapacity(e.target.value)}
                    required
                    data-testid="register-capacity"
                />

                <Input
                    label="Facility Location (Optional)"
                    value={facilityLocation}
                    onChange={(e) => setFacilityLocation(e.target.value)}
                    data-testid="register-location"
                />

                <div style={{ margin: "10px 0 16px 0", fontSize: "14px" }}>
                    <label style={{ display: "flex", gap: "8px", alignItems: "center", cursor: "pointer" }}>
                        <input
                            type="checkbox"
                            checked={agreeTerms}
                            onChange={(e) => setAgreeTerms(e.target.checked)}
                            required
                        />
                        Terms of Service ကို သဘောတူပါသည်။
                    </label>
                </div>

                {error && (
                    <div className={styles.error} role="alert" data-testid="register-error">
                        {error}
                    </div>
                )}

                <Button type="submit" disabled={submitting} data-testid="register-submit">
                    {submitting ? "Creating Account..." : "Register"}
                </Button>

                <div style={{ marginTop: "16px", textAlign: "center", fontSize: "14px" }}>
                    Already have an account? <Link to="/login">Sign In</Link>
                </div>
            </form>
        </div>
    );
}