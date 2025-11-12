async function postJson(url, payload){
    const res = await fetch(url, { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(payload)});
    return res;
}

function showConfirm(message){
    return new Promise((resolve)=>{
        const modalEl = document.getElementById('confirmModal');
        const msg = document.getElementById('confirmModalMessage');
        msg.innerText = message;
        const modal = new bootstrap.Modal(modalEl);
        const ok = document.getElementById('confirmOkBtn');
        const cancel = document.getElementById('confirmCancelBtn');

        function cleanup(){
            ok.removeEventListener('click', onOk);
            cancel.removeEventListener('click', onCancel);
            modal.hide();
        }
        function onOk(){ cleanup(); resolve(true); }
        function onCancel(){ cleanup(); resolve(false); }

        ok.addEventListener('click', onOk);
        cancel.addEventListener('click', onCancel);
        modal.show();
    });
}

// small toast/notification helper shown at top-right
function showToast(message, success = true, autoHideMs = 1200){
    // create container if missing
    let container = document.getElementById('toastContainer');
    if (!container){
        container = document.createElement('div');
        container.id = 'toastContainer';
        document.body.appendChild(container);
    }

    const toast = document.createElement('div');
    toast.className = 'app-toast ' + (success ? 'app-toast-success' : 'app-toast-error');
    toast.innerHTML = `<span class="app-toast-icon">${success ? '?' : '?'}</span><div class="app-toast-message">${message}</div>`;
    container.appendChild(toast);

    // animate in
    requestAnimationFrame(()=> { toast.classList.add('visible'); });

    if (autoHideMs > 0){
        setTimeout(()=>{
            toast.classList.remove('visible');
            setTimeout(()=> toast.remove(), 300);
        }, autoHideMs);
    }

    return toast;
}

async function deleteOrder(id){
    const ok = await showConfirm('Eliminar esta reservación definitivamente?');
    if (!ok) return;
    try{
        const res = await postJson('/Home/DeleteOrder', { Id: id });
        if (res.status === 401) { window.location.href = '/Home/IniciarSesion'; return; }
        if (res.ok){
            showToast('Reservación eliminada', true);
            setTimeout(()=> location.reload(), 900);
        }
        else { const txt = await res.text(); alert('Error eliminando: ' + (txt || res.statusText)); }
    } catch(e){ alert('Error eliminando: ' + e.message); }
}

async function cancelOrder(id){
    const ok = await showConfirm('¿Cancelar esta reservación?');
    if (!ok) return;
    try{
        const res = await postJson('/Home/CancelOrder', { Id: id });
        if (res.status === 401) { window.location.href = '/Home/IniciarSesion'; return; }
        if (res.ok){
            showToast('Reservación cancelada', true);
            setTimeout(()=> location.reload(), 900);
        }
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
                            showToast('Reservación actualizada', true);
                            setTimeout(()=> location.reload(), 900);
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
window.showToast = showToast;