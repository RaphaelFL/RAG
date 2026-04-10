'use client';

import * as React from 'react';
import type { CSSProperties } from 'react';
import clsx from 'clsx';
import type { ChunkDisplayMode, DocumentNode } from '@/features/documents/viewer/types';

export function DocumentNodeRenderer({
  nodes,
  mode,
  highlightRange,
  className
}: Readonly<{
  nodes: DocumentNode[];
  mode: ChunkDisplayMode;
  highlightRange?: { startOffset?: number; endOffset?: number } | null;
  className?: string;
}>) {
  return (
    <div className={clsx('structured-document-root', `structured-mode-${mode}`, className)}>
      {nodes.map((node) => (
        <RenderedNode key={node.id} highlightRange={highlightRange} mode={mode} node={node} />
      ))}
    </div>
  );
}

function RenderedNode({
  node,
  mode,
  highlightRange
}: Readonly<{
  node: DocumentNode;
  mode: ChunkDisplayMode;
  highlightRange?: { startOffset?: number; endOffset?: number } | null;
}>) {
  if (node.kind === 'text') {
    return renderTextNode(node, highlightRange);
  }

  if (node.kind === 'line-break') {
    return <br />;
  }

  if (node.kind === 'image') {
    return renderImageNode(node, mode, highlightRange);
  }

  return renderStructuredNode(node, mode, highlightRange);
}

function renderImageNode(
  node: Extract<DocumentNode, { kind: 'image' }>,
  mode: ChunkDisplayMode,
  highlightRange?: { startOffset?: number; endOffset?: number } | null
) {
  const highlighted = isNodeHighlighted(node, highlightRange);
  return (
    <figure
      className={clsx('structured-node', 'structured-image', highlighted && 'is-highlighted')}
      data-node-id={node.id}
      style={resolveNodeStyle(node, mode, highlighted)}
    >
      <img alt={node.alt ?? ''} loading="lazy" src={node.src} />
      {node.caption ? <figcaption>{node.caption}</figcaption> : null}
    </figure>
  );
}

function renderStructuredNode(
  node: Exclude<DocumentNode, { kind: 'text' | 'line-break' | 'image' }>,
  mode: ChunkDisplayMode,
  highlightRange?: { startOffset?: number; endOffset?: number } | null
) {
  const highlighted = isNodeHighlighted(node, highlightRange);
  const children = node.children.map((child) => (
    <RenderedNode key={child.id} highlightRange={highlightRange} mode={mode} node={child} />
  ));
  const commonProps = {
    className: clsx(
      'structured-node',
      `structured-${node.kind}`,
      node.layout?.position === 'absolute' && mode === 'fidelity' && 'is-absolute',
      highlighted && 'is-highlighted'
    ),
    style: resolveNodeStyle(node, mode, highlighted),
    'data-node-id': node.id,
    'data-outline-target': node.outlineId ?? undefined
  };

  switch (node.kind) {
    case 'heading': {
      const level = Math.min(Math.max(node.level ?? 2, 1), 6);
      if (level === 1) return <h1 {...commonProps}>{children}</h1>;
      if (level === 2) return <h2 {...commonProps}>{children}</h2>;
      if (level === 3) return <h3 {...commonProps}>{children}</h3>;
      if (level === 4) return <h4 {...commonProps}>{children}</h4>;
      if (level === 5) return <h5 {...commonProps}>{children}</h5>;
      return <h6 {...commonProps}>{children}</h6>;
    }
    case 'paragraph':
      return <p {...commonProps}>{children}</p>;
    case 'list':
      return node.ordered ? <ol {...commonProps}>{children}</ol> : <ul {...commonProps}>{children}</ul>;
    case 'list-item':
      return <li {...commonProps}>{children}</li>;
    case 'table':
      return <div className="structured-table-shell"><table {...commonProps}><tbody>{children}</tbody></table></div>;
    case 'table-row':
      return <tr {...commonProps}>{children}</tr>;
    case 'table-cell':
      return <td {...commonProps}>{children}</td>;
    case 'link':
      return <a {...commonProps} href={node.metadata?.href ?? '#'} rel="noreferrer" target="_blank">{children}</a>;
    case 'page':
    case 'section':
    case 'sheet':
    case 'slide':
    case 'document':
    case 'container':
    default:
      return <div {...commonProps}>{children}</div>;
  }
}

function renderTextNode(
  node: Extract<DocumentNode, { kind: 'text' }>,
  highlightRange?: { startOffset?: number; endOffset?: number } | null
) {
  const baseStyle = resolveNodeStyle(node, 'clean', false);
  const startOffset = node.sourceMap?.startOffset;
  const endOffset = node.sourceMap?.endOffset;
  if (startOffset === undefined || endOffset === undefined || highlightRange?.startOffset === undefined || highlightRange?.endOffset === undefined) {
    return <span className="structured-text" style={baseStyle}>{node.text}</span>;
  }

  const overlapStart = Math.max(startOffset, highlightRange.startOffset);
  const overlapEnd = Math.min(endOffset, highlightRange.endOffset);
  if (overlapEnd <= overlapStart) {
    return <span className="structured-text" style={baseStyle}>{node.text}</span>;
  }

  const localStart = overlapStart - startOffset;
  const localEnd = overlapEnd - startOffset;
  const before = node.text.slice(0, localStart);
  const highlightedText = node.text.slice(localStart, localEnd);
  const after = node.text.slice(localEnd);

  return (
    <span className="structured-text" style={baseStyle}>
      {before}
      <mark className="structured-inline-highlight">{highlightedText}</mark>
      {after}
    </span>
  );
}

function resolveNodeStyle(node: DocumentNode, mode: ChunkDisplayMode, highlighted: boolean): CSSProperties | undefined {
  const baseStyle = node.style?.css
    ? ({ ...node.style.css } as CSSProperties)
    : undefined;

  if (!baseStyle) {
    return highlighted && node.kind !== 'text'
      ? {
        boxShadow: '0 0 0 2px rgba(13, 148, 136, 0.32) inset',
        backgroundColor: 'rgba(13, 148, 136, 0.08)'
      }
      : undefined;
  }

  if (mode === 'clean') {
    delete baseStyle.position;
    delete baseStyle.left;
    delete baseStyle.top;
    delete baseStyle.width;
    delete baseStyle.height;
    delete baseStyle.minHeight;
    delete baseStyle.maxWidth;

    if (node.kind === 'page' || node.kind === 'slide') {
      baseStyle.width = '100%';
      baseStyle.position = 'relative';
    }
  }

  if (node.style?.preserveWhitespace && !baseStyle.whiteSpace) {
    baseStyle.whiteSpace = 'pre-wrap';
  }

  if (highlighted && node.kind !== 'text') {
    baseStyle.boxShadow = '0 0 0 2px rgba(13, 148, 136, 0.32) inset';
    baseStyle.backgroundColor = baseStyle.backgroundColor || 'rgba(13, 148, 136, 0.08)';
  }

  return Object.keys(baseStyle).length > 0 ? baseStyle : undefined;
}

function isNodeHighlighted(node: DocumentNode, highlightRange?: { startOffset?: number; endOffset?: number } | null) {
  const nodeStart = node.sourceMap?.startOffset;
  const nodeEnd = node.sourceMap?.endOffset;
  if (nodeStart === undefined || nodeEnd === undefined || highlightRange?.startOffset === undefined || highlightRange?.endOffset === undefined) {
    return false;
  }

  return nodeEnd > highlightRange.startOffset && nodeStart < highlightRange.endOffset;
}