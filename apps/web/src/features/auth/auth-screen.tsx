"use client";
import { useClientReady } from "@/lib/use-client-ready";
import Link from "next/link";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, Check, LifeBuoy } from "lucide-react";
import { Button } from "@/components/ui/button";
import { api, errorMessage } from "@/lib/api";
import { useAuth } from "./auth-provider";

type Mode = "login" | "register" | "forgot" | "reset";
const titles = {
  login: "Welcome back.",
  register: "A little help starts here.",
  forgot: "Let's get you back in.",
  reset: "Choose a fresh start.",
};
const descriptions = {
  login: "Sign in to keep the conversation moving.",
  register: "Create your account and connect with our support team.",
  forgot: "Enter your email and we'll send you a password reset link.",
  reset: "Set a new password to securely access your workspace.",
};

export function AuthScreen({
  mode,
  email = "",
  token = "",
}: {
  mode: Mode;
  email?: string;
  token?: string;
}) {
  const ready = useClientReady();
  const auth = useAuth();
  const router = useRouter();
  const [serverError, setServerError] = useState("");
  const [message, setMessage] = useState("");
  const schema = z.object({
    email: z.email("Enter a valid email."),
    password: z
      .string()
      .min(mode === "forgot" ? 0 : 12, "Use at least 12 characters.")
      .max(128),
    name:
      mode === "register"
        ? z.string().min(2, "Enter your name.").max(80)
        : z.string().optional(),
  });
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { email, password: "", name: "" },
  });
  const submit = handleSubmit(async (values) => {
    setServerError("");
    setMessage("");
    try {
      if (mode === "login")
        await auth.signIn({ email: values.email, password: values.password });
      else if (mode === "register")
        await auth.register({
          email: values.email,
          password: values.password,
          name: values.name ?? "",
        });
      else if (mode === "forgot") {
        await api("/auth/forgot-password", {
          method: "POST",
          body: JSON.stringify({ email: values.email }),
        });
        setMessage(
          "If an account exists, a reset link has been sent. Check your inbox.",
        );
      } else {
        await api("/auth/reset-password", {
          method: "POST",
          body: JSON.stringify({
            email: values.email,
            token,
            password: values.password,
          }),
        });
        auth.clearSession();
        setMessage("Your password has been reset. You can now sign in.");
        window.setTimeout(() => router.push("/login"), 1500);
      }
    } catch (error) {
      setServerError(errorMessage(error));
    }
  });

  return (
    <div className="auth-page">
      <aside className="auth-story">
        <Link href="/" className="workspace-brand">
          <span className="brand-mark">t</span>TicketFlow
        </Link>
        <div className="auth-story-copy">
          <span className="auth-kicker">A BETTER WAY TO SUPPORT</span>
          <h2>
            Every question deserves
            <br />a clear answer.
          </h2>
          <p>
            A shared space for the people asking for help and the teams making
            things right.
          </p>
          <div className="auth-flow">
            <span>
              <span className="flow-dot" />
              Question raised
            </span>
            <div />
            <span>
              <span className="flow-dot active" />
              Conversation started
            </span>
            <div />
            <span>
              <Check size={14} />
              Problem solved
            </span>
          </div>
        </div>
        <div className="auth-story-footer">
          <LifeBuoy size={18} />
          <span>Thoughtful support. From the ground up.</span>
        </div>
      </aside>
      <main className="auth-main">
        <div className="auth-topline">
          <Link href="/">← Back to TicketFlow</Link>
          <span>SUPPORT WORKSPACE</span>
        </div>
        <section className="auth-panel" aria-labelledby="auth-title">
          <span className="form-kicker">
            {mode === "register"
              ? "JOIN THE CONVERSATION"
              : "YOUR WORKSPACE, ONE STEP AWAY"}
          </span>
          <h1 id="auth-title">{titles[mode]}</h1>
          <p className="auth-description">{descriptions[mode]}</p>
          {mode === "login" && auth.sessionNotice && (
            <div role="alert" className="error-box">
              {auth.sessionNotice}
            </div>
          )}
          {message ? (
            <div className="success-box" role="status">
              <Check size={18} />
              <p>{message}</p>
              <Link href="/login">Back to sign in →</Link>
            </div>
          ) : (
            <form
              method="post"
              onSubmit={submit}
              noValidate
              className="auth-form"
            >
              <fieldset
                className="auth-fields"
                disabled={!ready || isSubmitting}
              >
                {mode === "register" && (
                  <div className="form-field">
                    <label htmlFor="name">Your name</label>
                    <input
                      id="name"
                      autoComplete="name"
                      placeholder="Alex Morgan"
                      {...register("name")}
                      aria-invalid={!!errors.name}
                      aria-describedby={errors.name ? "name-error" : undefined}
                    />
                    {errors.name && (
                      <p id="name-error" className="field-error">
                        {errors.name.message}
                      </p>
                    )}
                  </div>
                )}
                <div className="form-field">
                  <label htmlFor="email">Email address</label>
                  <input
                    id="email"
                    type="email"
                    autoComplete="email"
                    placeholder="you@example.com"
                    {...register("email")}
                    aria-invalid={!!errors.email}
                    aria-describedby={errors.email ? "email-error" : undefined}
                  />
                  {errors.email && (
                    <p id="email-error" className="field-error">
                      {errors.email.message}
                    </p>
                  )}
                </div>
                {mode !== "forgot" && (
                  <div className="form-field">
                    <div className="field-label-row">
                      <label htmlFor="password">
                        {mode === "reset" ? "New password" : "Password"}
                      </label>
                      {mode === "login" && (
                        <Link href="/forgot-password">Forgot password?</Link>
                      )}
                    </div>
                    <input
                      id="password"
                      type="password"
                      autoComplete={
                        mode === "login" ? "current-password" : "new-password"
                      }
                      placeholder="At least 12 characters"
                      {...register("password")}
                      aria-invalid={!!errors.password}
                      aria-describedby={
                        errors.password ? "password-error" : undefined
                      }
                    />
                    {errors.password && (
                      <p id="password-error" className="field-error">
                        {errors.password.message}
                      </p>
                    )}
                    {mode !== "login" && (
                      <p className="field-hint">
                        Use upper/lowercase letters, a number, and a symbol.
                      </p>
                    )}
                  </div>
                )}
                {serverError && (
                  <div role="alert" className="error-box">
                    {serverError}
                  </div>
                )}
                <Button
                  type="submit"
                  className="auth-submit"
                  disabled={isSubmitting || (mode === "reset" && !token)}
                >
                  {isSubmitting
                    ? "Please wait…"
                    : mode === "login"
                      ? "Sign in"
                      : mode === "register"
                        ? "Create account"
                        : mode === "forgot"
                          ? "Send reset link"
                          : "Reset password"}
                  <ArrowRight size={16} />
                </Button>
                {mode === "reset" && !token && (
                  <p role="alert" className="field-error">
                    Open the reset link from your email to continue.
                  </p>
                )}
              </fieldset>
            </form>
          )}
          <p className="auth-switch">
            {mode === "login" ? (
              <>
                New to TicketFlow?{" "}
                <Link href="/register">Create an account</Link>
              </>
            ) : (
              <>
                Already have an account? <Link href="/login">Sign in</Link>
              </>
            )}
          </p>
          <div className="auth-trust">
            <span className="flow-dot" /> A private space for your support
            conversations.
          </div>
        </section>
        <p className="auth-copyright">
          © 2026 TicketFlow. A little clarity goes a long way.
        </p>
      </main>
    </div>
  );
}
