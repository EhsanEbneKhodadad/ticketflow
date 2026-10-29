"use client";
import { useClientReady } from "@/lib/use-client-ready";
import { Link, useRouter } from "@/i18n/navigation";
import { useState } from "react";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowRight, Check, LifeBuoy } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  postApiV1AuthForgotPasswordMutation,
  postApiV1AuthResetPasswordMutation,
} from "@ticketflow/api-client";
import { useErrorMessage } from "@/lib/use-error-message";
import { useAuth } from "./auth-provider";
import { LanguageSwitcher } from "@/components/language-switcher";

type Mode = "login" | "register" | "forgot" | "reset";
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
  const t = useTranslations("Auth");
  const errorMessage = useErrorMessage();
  const [serverError, setServerError] = useState("");
  const [message, setMessage] = useState("");
  const schema = z.object({
    email: z.email(t("validEmail")),
    password: z
      .string()
      .min(mode === "forgot" ? 0 : 12, t("passwordMin"))
      .max(128),
    name:
      mode === "register"
        ? z.string().min(2, t("nameRequired")).max(80)
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
        await postApiV1AuthForgotPasswordMutation().mutationFn!(
          {
            body: { email: values.email },
            throwOnError: true,
          },
          undefined as never,
        );
        setMessage(t("checkEmail"));
      } else {
        await postApiV1AuthResetPasswordMutation().mutationFn!(
          {
            body: {
              email: values.email,
              token,
              password: values.password,
            },
            throwOnError: true,
          },
          undefined as never,
        );
        auth.clearSession();
        setMessage(t("resetDone"));
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
          <span className="auth-kicker">{t("kicker")}</span>
          <h2>{t("storyTitle")}</h2>
          <p>{t("storyDescription")}</p>
          <div className="auth-flow">
            <span>
              <span className="flow-dot" />
              {t("questionRaised")}
            </span>
            <div />
            <span>
              <span className="flow-dot active" />
              {t("conversationStarted")}
            </span>
            <div />
            <span>
              <Check size={14} />
              {t("problemSolved")}
            </span>
          </div>
        </div>
        <div className="auth-story-footer">
          <LifeBuoy size={18} />
          <span>{t("privateSpace")}</span>
        </div>
      </aside>
      <main className="auth-main">
        <div className="auth-topline">
          <Link href="/">{t("back")}</Link>
          <span>{t("topline")}</span>
          <LanguageSwitcher />
        </div>
        <section className="auth-panel" aria-labelledby="auth-title">
          <span className="form-kicker">
            {mode === "register" ? t("join") : t("oneStep")}
          </span>
          <h1 id="auth-title">{t(`${mode}Title`)}</h1>
          <p className="auth-description">{t(`${mode}Description`)}</p>
          {mode === "login" && auth.sessionNotice && (
            <div role="alert" className="error-box">
              {auth.sessionNotice}
            </div>
          )}
          {message ? (
            <div className="success-box" role="status">
              <Check size={18} />
              <p>{message}</p>
              <Link href="/login">{t("signIn")} →</Link>
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
                    <label htmlFor="name">{t("name")}</label>
                    <input
                      id="name"
                      autoComplete="name"
                      placeholder={t("namePlaceholder")}
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
                  <label htmlFor="email">{t("email")}</label>
                  <input
                    id="email"
                    type="email"
                    autoComplete="email"
                    placeholder={t("emailPlaceholder")}
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
                        {mode === "reset" ? t("newPassword") : t("password")}
                      </label>
                      {mode === "login" && (
                        <Link href="/forgot-password">
                          {t("forgotPassword")}
                        </Link>
                      )}
                    </div>
                    <input
                      id="password"
                      type="password"
                      autoComplete={
                        mode === "login" ? "current-password" : "new-password"
                      }
                      placeholder={t("passwordPlaceholder")}
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
                      <p className="field-hint">{t("passwordHint")}</p>
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
                    ? t("wait")
                    : mode === "login"
                      ? t("signIn")
                      : mode === "register"
                        ? t("createAccount")
                        : mode === "forgot"
                          ? t("sendReset")
                          : t("resetPassword")}
                  <ArrowRight size={16} />
                </Button>
                {mode === "reset" && !token && (
                  <p role="alert" className="field-error">
                    {t("missingToken")}
                  </p>
                )}
              </fieldset>
            </form>
          )}
          <p className="auth-switch">
            {mode === "login" ? (
              <>
                {t("newUser")}{" "}
                <Link href="/register">{t("createAccount")}</Link>
              </>
            ) : (
              <>
                {t("existingUser")} <Link href="/login">{t("signIn")}</Link>
              </>
            )}
          </p>
          <div className="auth-trust">
            <span className="flow-dot" /> {t("privateSpace")}
          </div>
        </section>
        <p className="auth-copyright">{t("copyright")}</p>
      </main>
    </div>
  );
}
