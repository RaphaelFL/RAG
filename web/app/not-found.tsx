export default function NotFound() {
  return (
    <main className="console-shell">
      <section className="panel">
        <div className="panel-header">
          <div>
            <h2>Pagina nao encontrada</h2>
            <p>O endereco solicitado nao existe nesta interface.</p>
          </div>
        </div>

        <div className="button-row">
          <a className="button secondary" href="/">
            Voltar ao console
          </a>
        </div>
      </section>
    </main>
  );
}