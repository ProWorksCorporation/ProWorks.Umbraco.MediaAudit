// Minimal, dependency-free fetch stub - MediaAuditRepository (media-audit.repository.ts) is a
// plain object of functions that all funnel through one module-level `request()` helper calling
// global `fetch()` directly, so stubbing `globalThis.fetch` is sufficient to test every element
// that calls into the repository, without needing a module-mocking library.

export type FetchHandler = (url: string, init: RequestInit | undefined) => Response | Promise<Response>;

/** Replaces globalThis.fetch for the duration of a test; call the returned function to restore it. */
export function stubFetch(handler: FetchHandler): () => void {
  const original = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.toString() : input.url;
    return handler(url, init);
  }) as typeof fetch;

  return () => {
    globalThis.fetch = original;
  };
}

export function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}
