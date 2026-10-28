"use client";
import Link from "next/link";
import { useEffect, useState, type ReactNode } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  Inbox,
  Plus,
  UserRound,
  CircleDashed,
  LifeBuoy,
  LogOut,
  Menu,
  Search,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { errorMessage } from "@/lib/api";
import { initials } from "@/features/tickets/api";
import { useAuth } from "./auth-provider";

export function WorkspaceShell({ children }: { children: ReactNode }) {
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
          <h2>We could not reach your workspace.</h2>
          <p>{errorMessage(auth.error)}</p>
          <Button onClick={auth.retry}>Try again</Button>
        </div>
      </div>
    );
  if (auth.loading || !auth.user)
    return (
      <div className="loading-screen" role="status">
        <span className="spinner" />
        Opening your workspace…
      </div>
    );
  const user = auth.user;
  const staff = user.role !== "Customer";
  return (
    <div className="workspace-root">
      <a className="skip-link" href="#workspace-main">
        Skip to content
      </a>
      {open && (
        <button
          className="sidebar-scrim"
          aria-label="Close navigation"
          onClick={() => setOpen(false)}
        />
      )}
      <aside
        className={"workspace-sidebar" + (open ? " nav-open" : "")}
        aria-label="Workspace sidebar"
      >
        <Link
          className="workspace-brand"
          href="/tickets"
          onClick={() => setOpen(false)}
        >
          <span className="brand-mark">t</span>TicketFlow
        </Link>
        <nav className="workspace-nav" aria-label="Workspace navigation">
          <p className="nav-label">WORKSPACE</p>
          <Link
            href="/tickets"
            className={pathname === "/tickets" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <Inbox size={16} />
            {staff ? "All tickets" : "Your tickets"}
          </Link>
          <Link
            href="/tickets/new"
            className={pathname === "/tickets/new" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <Plus size={16} />
            New ticket
          </Link>
          <Link
            href="/search"
            className={pathname === "/search" ? "active" : ""}
            onClick={() => setOpen(false)}
          >
            <Search size={16} />
            Search tickets
          </Link>
          {staff && (
            <>
              <p className="nav-label">YOUR QUEUES</p>
              <Link href="/tickets?view=mine" onClick={() => setOpen(false)}>
                <UserRound size={16} />
                Assigned to me
              </Link>
              <Link
                href="/tickets?view=unassigned"
                onClick={() => setOpen(false)}
              >
                <CircleDashed size={16} />
                Unassigned
              </Link>
            </>
          )}
          <p className="nav-label">RESOURCES</p>
          <Link href="/" onClick={() => setOpen(false)}>
            <LifeBuoy size={16} />
            About TicketFlow
          </Link>
        </nav>
        <div className="nav-spacer" />
        <div className="sidebar-note">
          <strong>A little clarity goes a long way.</strong>Keep every question,
          reply, and next step in one place.
        </div>
        <div className="sidebar-account">
          <span className="avatar">{initials(user.name)}</span>
          <div>
            <strong>{user.name}</strong>
            <small>
              {user.role === "Admin"
                ? "Administrator"
                : user.role === "Agent"
                  ? "Support agent"
                  : "Customer"}
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
          Sign out
        </button>
      </aside>
      <header className="workspace-header">
        <div className="workspace-breadcrumb">
          <button
            className="mobile-menu-button"
            aria-label="Open navigation"
            aria-expanded={open}
            onClick={() => setOpen(true)}
          >
            <Menu size={19} />
          </button>
          <span>TicketFlow</span>
          <span>/</span>
          <strong>{staff ? "Support workspace" : "Your workspace"}</strong>
        </div>
        <span className="role-pill">
          <span className="flow-dot" />
          {user.role === "Admin" ? "Administrator" : user.role}
        </span>
      </header>
      <main key={user.id} className="workspace-main" id="workspace-main">
        {children}
      </main>
    </div>
  );
}
