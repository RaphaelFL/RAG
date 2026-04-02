import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'RAG Console',
  description: 'Frontend do chatbot corporativo com RAG, streaming, upload e historico.',
  icons: {
    icon: '/favicon.ico'
  }
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="pt-BR">
      <body>
        {children}
      </body>
    </html>
  );
}
