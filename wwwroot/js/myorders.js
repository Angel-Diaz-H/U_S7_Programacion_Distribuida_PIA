async function postJson(url, payload){
    const res = await fetch(url, { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(payload)});
    return res;
}

async function deleteOrder(id){
    if(!confirm('¿Eliminar esta reservación definitivamente?')) return;
    try{
        const res = await postJson('/Home/DeleteOrder', { Id: id });
        if (res.status === 401) { window.location.href = '/Home/IniciarSesion'; return; }
        if (res.ok) location.reload();
        else { const txt = await res.text(); alert('Error eliminando: ' + (txt || res.statusText)); }
    } catch(e){ alert('Error eliminando: ' + e.message); }
}

async function cancelOrder(id){
    if(!confirm('¿Cancelar esta reservación?')) return;
    try{
        const res = await postJson('/Home/CancelOrder', { Id: id });
        if (res.status === 401) { window.location.href = '/Home/IniciarSesion'; return; }
        if (res.ok) location.reload();
        else { const txt = await res.text(); alert('Error cancelando: ' + (txt || res.statusText)); console.error('CancelOrder failed', res.status, txt); }
    } catch(e){ alert('Error cancelando: ' + e.message); }
}

function openEdit(id){
    fetch('/Home/EditOrder?id=' + id).then(r=>{
        if (r.status === 401) { window.location.href = '/Home/IniciarSesion'; return null; }
        return r.text();
    }).then(html=>{
        if (!html) return;
        const modalDiv = document.createElement('div');
        modalDiv.innerHTML = html;
        document.body.appendChild(modalDiv);
        var modalEl = modalDiv.querySelector('.modal');
        var modal = new bootstrap.Modal(modalEl);
        modal.show();

        // attach save handler after modal is shown
        modalEl.addEventListener('shown.bs.modal', ()=>{
            const saveBtn = modalEl.querySelector('#saveEditBtn');
            if (saveBtn){
                saveBtn.addEventListener('click', async function handler(ev){
                    // prevent double binding
                    saveBtn.removeEventListener('click', handler);
                    const form = modalEl.querySelector('#editOrderForm');
                    if (!form) { alert('Formulario no encontrado'); return; }
                    const data = new FormData(form);
                    const payload = {
                        Id: parseInt(data.get('id')),
                        Hour: data.get('hour'),
                        Persons: parseInt(data.get('persons')),
                        Notes: data.get('notes') || ''
                    };
                    try{
                        const res = await postJson('/Home/EditOrderSubmit', payload);
                        if (res.status === 401) { window.location.href = '/Home/IniciarSesion'; return; }
                        if (res.ok){
                            modal.hide();
                            location.reload();
                        } else {
                            const txt = await res.text();
                            alert('Error guardando: ' + (txt || res.statusText));
                            console.error('EditOrderSubmit failed', res.status, txt);
                        }
                    } catch(e){ alert('Error guardando: ' + e.message); }
                });
            }
        });

        modalEl.addEventListener('hidden.bs.modal', ()=> modalDiv.remove());
    });
}

// attach to global window so Razor inline buttons can call
window.deleteOrder = deleteOrder;
window.cancelOrder = cancelOrder;
window.openEdit = openEdit;