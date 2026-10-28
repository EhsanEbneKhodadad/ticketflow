"use client";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { components } from "@ticketflow/api-client";
import { useAuth, type User } from "@/features/auth/auth-provider";
import { api, errorMessage } from "@/lib/api";
import { Button } from "@/components/ui/button";

type Category = components["schemas"]["AdminCategoryView"];
type AdminUser = components["schemas"]["AdminUserView"];
type CategoryRequest = components["schemas"]["CategoryRequest"];
type RoleRequest = components["schemas"]["ChangeRoleRequest"];
const categorySchema = z.object({
  name: z.string().trim().min(1, "Enter a name.").max(100),
  slug: z.string().trim().min(1, "Enter a slug.").max(100),
});
type CategoryFields = z.infer<typeof categorySchema>;

export function Admin() {
  const { user } = useAuth();
  const client = useQueryClient();
  const principal = user?.id;
  const enabled = user?.role === "Admin";
  const isCurrent = () =>
    !!principal && client.getQueryData<User>(["session"])?.id === principal;
  const options = { expectedPrincipalId: principal, isCurrent };
  const categories = useQuery({
    queryKey: ["admin", principal, "categories"],
    enabled,
    retry: false,
    queryFn: ({ signal }) =>
      api<Category[]>("/admin/categories", { ...options, signal }),
  });
  const users = useQuery({
    queryKey: ["admin", principal, "users"],
    enabled,
    retry: false,
    queryFn: ({ signal }) =>
      api<AdminUser[]>("/admin/users", { ...options, signal }),
  });
  if (!enabled)
    return (
      <section>
        <h1>Administration</h1>
        <p role="alert">Administrator access required.</p>
      </section>
    );
  return (
    <section>
      <div className="page-heading">
        <div>
          <p className="page-eyebrow">WORKSPACE SETTINGS</p>
          <h1>Administration</h1>
          <p>Manage ticket categories and account roles.</p>
        </div>
      </div>
      <section className="card admin-section">
        <h2>Categories</h2>
        <CategoryForm
          key="new"
          options={options}
          onSaved={() =>
            void client.invalidateQueries({
              queryKey: ["admin", principal, "categories"],
            })
          }
        />
        {categories.isPending && <p role="status">Loading categories…</p>}
        {categories.isError && (
          <div role="alert">
            {errorMessage(categories.error)}{" "}
            <Button onClick={() => void categories.refetch()}>Try again</Button>
          </div>
        )}
        {categories.data?.length === 0 && <p>No categories yet.</p>}
        {categories.data?.map((category) => (
          <CategoryForm
            key={category.id}
            category={category}
            options={options}
            onSaved={() =>
              void client.invalidateQueries({
                queryKey: ["admin", principal, "categories"],
              })
            }
          />
        ))}
      </section>
      <section className="card admin-section">
        <h2>Users and roles</h2>
        {users.isPending && <p role="status">Loading users…</p>}
        {users.isError && (
          <div role="alert">
            {errorMessage(users.error)}{" "}
            <Button onClick={() => void users.refetch()}>Try again</Button>
          </div>
        )}
        {users.data?.length === 0 && <p>No users found.</p>}
        {users.data?.map((account) => (
          <UserRole
            key={account.id}
            account={account}
            currentId={principal!}
            options={options}
            onSaved={() =>
              void client.invalidateQueries({
                queryKey: ["admin", principal, "users"],
              })
            }
          />
        ))}
      </section>
    </section>
  );
}
type Options = { expectedPrincipalId?: string; isCurrent: () => boolean };
function CategoryForm({
  category,
  options,
  onSaved,
}: {
  category?: Category;
  options: Options;
  onSaved: () => void;
}) {
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<CategoryFields>({
    resolver: zodResolver(categorySchema),
    defaultValues: { name: category?.name ?? "", slug: category?.slug ?? "" },
  });
  const [notice, setNotice] = useState("");
  const mutation = useMutation({
    mutationFn: (input: CategoryRequest) =>
      api<Category>(
        category ? `/admin/categories/${category.id}` : "/admin/categories",
        {
          ...options,
          method: category ? "PUT" : "POST",
          body: JSON.stringify(input),
        },
      ),
    onSuccess: (value) => {
      reset({
        name: category ? value.name : "",
        slug: category ? value.slug : "",
      });
      setNotice(category ? "Category saved." : "Category created.");
      onSaved();
    },
  });
  const prefix = category?.id ?? "new";
  return (
    <form
      className="admin-row"
      onSubmit={handleSubmit((input) => {
        setNotice("");
        mutation.mutate(input);
      })}
    >
      <div className="form-field">
        <label htmlFor={`name-${prefix}`}>
          {category ? `Name for ${category.name}` : "New category name"}
        </label>
        <input
          id={`name-${prefix}`}
          {...register("name")}
          aria-invalid={!!errors.name}
        />
        {errors.name && (
          <span className="field-error" role="alert">
            {errors.name.message}
          </span>
        )}
      </div>
      <div className="form-field">
        <label htmlFor={`slug-${prefix}`}>
          {category ? `Slug for ${category.name}` : "New category slug"}
        </label>
        <input
          id={`slug-${prefix}`}
          {...register("slug")}
          aria-invalid={!!errors.slug}
        />
        {errors.slug && (
          <span className="field-error" role="alert">
            {errors.slug.message}
          </span>
        )}
      </div>
      <Button disabled={mutation.isPending}>
        {category ? "Save category" : "Create category"}
      </Button>
      {mutation.isError && (
        <p role="alert" className="field-error">
          {errorMessage(mutation.error)}
        </p>
      )}
      {notice && <p role="status">{notice}</p>}
    </form>
  );
}
function UserRole({
  account,
  currentId,
  options,
  onSaved,
}: {
  account: AdminUser;
  currentId: string;
  options: Options;
  onSaved: () => void;
}) {
  const [role, setRole] = useState(account.role);
  const [notice, setNotice] = useState("");
  const mutation = useMutation({
    mutationFn: (input: RoleRequest) =>
      api<AdminUser>(`/admin/users/${account.id}/role`, {
        ...options,
        method: "PATCH",
        body: JSON.stringify(input),
      }),
    onSuccess: () => {
      setNotice("Role updated.");
      onSaved();
    },
  });
  return (
    <div className="admin-user">
      <div>
        <strong>{account.name}</strong>
        <p>{account.email}</p>
      </div>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          setNotice("");
          mutation.mutate({ role });
        }}
      >
        <label htmlFor={`role-${account.id}`}>Role for {account.name}</label>
        <select
          id={`role-${account.id}`}
          value={role}
          onChange={(event) => setRole(event.target.value)}
          disabled={account.id === currentId}
        >
          <option value="Customer">Customer</option>
          <option value="Agent">Agent</option>
          <option value="Admin">Admin</option>
        </select>
        <Button
          disabled={
            account.id === currentId ||
            role === account.role ||
            mutation.isPending
          }
        >
          Save role
        </Button>
        {account.id === currentId && (
          <small>You cannot change your own role.</small>
        )}
        {mutation.isError && (
          <span role="alert" className="field-error">
            {errorMessage(mutation.error)}
          </span>
        )}
        {notice && <span role="status">{notice}</span>}
      </form>
    </div>
  );
}
