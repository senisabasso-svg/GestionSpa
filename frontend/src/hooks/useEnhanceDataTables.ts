import { useEffect } from 'react';

/** Mejora todas las tablas .data-table: resize de columnas y botón Ampliar. */
export function useEnhanceDataTables(rootSelector = '.main-content') {
  useEffect(() => {
    const root = document.querySelector(rootSelector) ?? document.body;

    const enhanceContainer = (container: HTMLElement) => {
      const table = container.querySelector('table.data-table') as HTMLTableElement | null;
      if (!table) return;

      // Toolbar: ampliar / compactar
      if (!container.querySelector('.table-view-toggle')) {
        const bar = document.createElement('div');
        bar.className = 'table-view-toggle';
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'btn btn-sm btn-secondary';
        btn.textContent = 'Ampliar columnas';
        btn.title = 'Muestra el texto completo (nombre, documento, etc.)';
        btn.addEventListener('click', () => {
          const on = container.classList.toggle('table-expanded');
          btn.textContent = on ? 'Vista compacta' : 'Ampliar columnas';
        });
        const hint = document.createElement('span');
        hint.className = 'table-view-hint';
        hint.textContent = 'Arrastrá el borde derecho de cada encabezado para ajustar el ancho';
        bar.append(btn, hint);
        container.insertBefore(bar, container.firstChild);
      }

      enhanceResizers(table);
    };

    const enhanceResizers = (table: HTMLTableElement) => {
      if (table.dataset.resizable === '1') return;
      table.dataset.resizable = '1';

      const headers = table.querySelectorAll('thead th');
      headers.forEach((th, index) => {
        const el = th as HTMLElement;
        if (el.querySelector('.col-resizer')) return;

        const grip = document.createElement('span');
        grip.className = 'col-resizer';
        grip.title = 'Arrastrar para cambiar ancho';
        grip.addEventListener('mousedown', (e) => {
          e.preventDefault();
          e.stopPropagation();
          const startX = e.clientX;
          const startWidth = el.offsetWidth;

          const onMove = (ev: MouseEvent) => {
            const width = Math.max(72, startWidth + (ev.clientX - startX));
            el.style.width = `${width}px`;
            el.style.minWidth = `${width}px`;
            el.style.maxWidth = `${width}px`;
            // Alinear celdas del mismo índice
            table.querySelectorAll('tbody tr').forEach(row => {
              const cell = row.children[index] as HTMLElement | undefined;
              if (cell) {
                cell.style.width = `${width}px`;
                cell.style.minWidth = `${width}px`;
                cell.style.maxWidth = `${width}px`;
              }
            });
          };

          const onUp = () => {
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
            document.body.classList.remove('col-resizing');
          };

          document.body.classList.add('col-resizing');
          document.addEventListener('mousemove', onMove);
          document.addEventListener('mouseup', onUp);
        });

        el.appendChild(grip);
      });
    };

    const scan = () => {
      root.querySelectorAll<HTMLElement>('.table-container').forEach(enhanceContainer);
    };

    scan();
    const mo = new MutationObserver(() => scan());
    mo.observe(root, { childList: true, subtree: true });
    return () => mo.disconnect();
  }, [rootSelector]);
}
