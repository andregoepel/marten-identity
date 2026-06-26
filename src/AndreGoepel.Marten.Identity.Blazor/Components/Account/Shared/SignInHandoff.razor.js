// Submits the hidden sign-in handoff form as a top-level POST so the browser
// navigates to the cookie-sign-in middleware (which sets the auth cookie and
// redirects). Blazor Server always has JS available, so this runs reliably.
export function submitForm(form) {
    if (form) {
        form.submit();
    }
}
