import DocumentInspectorDetailConsole from '@/features/documents/components/DocumentInspectorDetailConsole';

export default async function DocumentInspectionDetailPage({
  params
}: Readonly<{
  params: Promise<{ documentId: string }>;
}>) {
  const { documentId } = await params;
  return <DocumentInspectorDetailConsole documentId={documentId} />;
}