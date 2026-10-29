"use client";
import { Link, usePathname, useRouter } from "@/i18n/navigation";
import { useTranslations } from "next-intl";
import { useEffect, useState, type ReactNode } from "react";
import {
  Inbox,
  Plus,
  UserRound,
  CircleDashed,
  LifeBuoy,
  LogOut,
  Menu,
  Search,
  LayoutDashboard,
  Settings,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { useErrorMessage } from "@/lib/use-error-message";
import { initials } from "@/features/tickets/api";
import { useAuth } from "./auth-provider";
import { LanguageSwitcher } from "@/components/language-switcher";

export function WorkspaceShell({ children }: { children: ReactNode }) {
  const t = useTranslations("Workspace");
  const errorMessage = useErrorMessage();
  const common = useTranslations("Dashboard");
  const auth = useAuth();
  const router = useRouter();
  const pathname = usePathname();
  const [open, setOpen] = useState(false);
  const [logoutError, setLogoutError] = useState("");
  useEffect(() => {
    if (!auth.loading && !auth.error && !auth.user) router.replace("/login");
  }, [auth.loading, auth.error, auth.user, router]);
  useEffect(() => {
    const escape = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", escape);
    return () => window.removeEventListener("keydown", escape);
  }, []);
  if (auth.error)
    return (
      <div className="loading-screen">
        <div className="page-state">
          <h2>{t("supportWorkspace")}</h2>
          <p>{errorMessage(auth.error)}</p>
          <Button onClick={auth.retry}>{common("retry")}</Button>
        </div>
      </div>
    );
  if (auth.loading || !auth.user)
    return (
      <div className="loading-screen" role="status">
        <span className="spinner" />
        {t("opening")}
      </div>
    );
  const user = auth.user;
  const staff = user.role !== "Customer";
  return (
    <div className="workspace-root">
      <a className="skip-link" href="#workspace-main">
        {t("skip")}
      </a>
      {open && (
        <button
          className="sidebar-scrim"
          aria-label={t("closeNavigation")}
          onClick={() => setOpen(false)}
        />
      )}
      <aside
        className={"workspace-sidebar" + (open ? " nav-open" : "")}
        aria-label={t("sidebar")}
      >
        <Link
          className="workspace-brand"
          href="/tickets"
          onClick={() => setOpen(false)}
        >
          <span className="brand-mark">t</span>TicketFlow
        </Link>
        <nav className="workspace-nav" aria-label={t("navigation")}>
          <p className="nav-label">{t("workspace")}</p>
          <Link
            href="/dashboard"
            className={pathname === "/dashboard" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <LayoutDashboard size={16} />
            {t("dashboard")}
          </Link>
          <Link
            href="/tickets"
            className={pathname === "/tickets" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <Inbox size={16} />
            {staff ? t("allTickets") : t("yourTickets")}
          </Link>
          <Link
            href="/tickets/new"
            className={pathname === "/tickets/new" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <Plus size={16} />
            {t("newTicket")}
          </Link>
          <Link
            href="/search"
            className={pathname === "/search" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <Search size={16} />
            {t("searchTickets")}
          </Link>
          {staff && (
            <>
              <p className="nav-label">{t("yourQueues")}</p>
              <Link href="/tickets?view=mine" onClick={() => setOpen(false)}>
                <UserRound size={16} />
                {t("assigned")}
              </Link>
              <Link
                href="/tickets?view=unassigned"
                onClick={() => setOpen(false)}
              >
                <CircleDashed size={16} />
                {t("unassigned")}
              </Link>
            </>
          )}
          {user.role === "Admin" && (
            <Link
              href="/admin"
              className={pathname === "/admin" ? "active" : ""}
              onClick={() => setOpen(false)}
            >
              <Settings size={16} />
              {t("administration")}
            </Link>
          )}
          <p className="nav-label">{t("resources")}</p>
          <Link href="/" onClick={() => setOpen(false)}>
            <LifeBuoy size={16} />
            {t("about")}
          </Link>
        </nav>
        <div className="nav-spacer" />
        <div className="sidebar-note">
          <strong>{t("noteTitle")}</strong> {t("note")}
        </div>
        <div className="sidebar-account">
          <span className="avatar">{initials(user.name)}</span>
          <div>
            <strong>{user.name}</strong>
            <small>
              {user.role === "Admin"
                ? t("administrator")
                : user.role === "Agent"
                  ? t("agent")
                  : t("customer")}
            </small>
          </div>
        </div>
        {logoutError && (
          <p role="alert" className="field-error">
            {logoutError}
          </p>
        )}
        <button
          className="signout-button"
          onClick={() => {
            setLogoutError("");
            void auth.signOut().catch((e) => setLogoutError(errorMessage(e)));
          }}
        >
          <LogOut size={14} />
          {t("signOut")}
        </button>
      </aside>
      <header className="workspace-header">
        <div className="workspace-breadcrumb">
          <button
            className="mobile-menu-button"
            aria-label={t("openNavigation")}
            aria-expanded={open}
            onClick={() => setOpen(true)}
          >
            <Menu size={19} />
          </button>
          <span>TicketFlow</span>
          <span>/</span>
          <strong>{staff ? t("supportWorkspace") : t("yourWorkspace")}</strong>
        </div>
        <span className="role-pill">
          <span className="flow-dot" />
          {user.role === "Admin"
            ? t("administrator")
            : user.role === "Agent"
              ? t("agent")
              : t("customer")}
        </span>
        <LanguageSwitcher />
      </header>
      <main key={user.id} className="workspace-main" id="workspace-main">
        {children}
      </main>
    </div>
  );
}
