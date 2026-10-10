export default function Loading() {
  return (
    <div className="space-y-6" aria-busy="true">
      <div className="space-y-1">
        <h1 className="text-3xl font-semibold tracking-tight">Our menu</h1>
        <p className="text-muted-foreground" role="status">
          Brewing the menu… if it&apos;s been a while, our server may take up to a minute to wake up.
        </p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {Array.from({ length: 8 }, (_, i) => (
          <div key={i} className="h-72 animate-pulse rounded-xl bg-muted" />
        ))}
      </div>
    </div>
  );
}