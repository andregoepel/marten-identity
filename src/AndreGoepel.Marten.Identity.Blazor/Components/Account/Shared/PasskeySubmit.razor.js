const isPasskeySupported =
    typeof navigator.credentials !== 'undefined' &&
    typeof window.PublicKeyCredential !== 'undefined' &&
    typeof window.PublicKeyCredential.parseCreationOptionsFromJSON === 'function' &&
    typeof window.PublicKeyCredential.parseRequestOptionsFromJSON === 'function';

let conditionalAbortController = null;

export function isSupported() {
    return isPasskeySupported;
}

export async function isConditionalMediationAvailable() {
    if (!isPasskeySupported) return false;
    return await (PublicKeyCredential.isConditionalMediationAvailable?.() ?? false);
}

// Encode an ArrayBuffer/typed array as an unpadded base64url string — the wire
// format ASP.NET Core Identity's BufferSource converter expects.
function bufferToBase64Url(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.length; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// Serialize a PublicKeyCredential into the exact JSON shape ASP.NET Core Identity
// deserializes on the server (PublicKeyCredential<TResponse>), independent of the
// browser's PublicKeyCredential.toJSON().
//
// Why not just JSON.stringify(credential)? That relies on the browser implementing
// the spec toJSON() method, which JSON.stringify invokes implicitly. Some browsers
// expose the static parse* helpers (so the feature check passes) while shipping an
// absent or incomplete toJSON(). In that case JSON.stringify serializes the
// credential's non-enumerable prototype getters as an empty/partial object, dropping
// required members — most visibly 'clientExtensionResults'. The server then rejects
// the payload with "The attestation credential JSON had an invalid format: ...
// missing required properties ... 'clientExtensionResults'". Building the payload
// explicitly guarantees every required member is present in every browser.
function credentialToJson(credential) {
    const response = credential.response;
    const clientExtensionResults =
        credential.getClientExtensionResults?.() ?? {};

    const json = {
        id: credential.id,
        rawId: bufferToBase64Url(credential.rawId),
        type: credential.type,
        authenticatorAttachment: credential.authenticatorAttachment ?? undefined,
        clientExtensionResults,
    };

    if ('attestationObject' in response) {
        // Registration ceremony (navigator.credentials.create).
        json.response = {
            clientDataJSON: bufferToBase64Url(response.clientDataJSON),
            attestationObject: bufferToBase64Url(response.attestationObject),
            transports: response.getTransports?.() ?? [],
        };
    } else {
        // Authentication ceremony (navigator.credentials.get).
        json.response = {
            clientDataJSON: bufferToBase64Url(response.clientDataJSON),
            authenticatorData: bufferToBase64Url(response.authenticatorData),
            signature: bufferToBase64Url(response.signature),
            userHandle: response.userHandle ? bufferToBase64Url(response.userHandle) : null,
        };
    }

    return json;
}

async function fetchOptions(optionsEndpointUrl) {
    const response = await fetch(optionsEndpointUrl, {
        method: 'POST',
        credentials: 'include',
    });
    if (!response.ok) throw new Error(`Failed to fetch passkey options: ${response.status}`);
    return await response.text();
}

async function postCredential(actionEndpointUrl, credential) {
    const response = await fetch(actionEndpointUrl, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(credentialToJson(credential)),
    });
    const text = await response.text();
    if (!response.ok) throw new Error(text || `Request failed: ${response.status}`);
    return text;
}

export async function requestCredential(optionsEndpointUrl, actionEndpointUrl) {
    if (!isPasskeySupported) throw new Error('Passkeys are not supported in this browser.');
    cancelConditionalMediation();
    const optionsJson = await fetchOptions(optionsEndpointUrl);
    const options = PublicKeyCredential.parseRequestOptionsFromJSON(JSON.parse(optionsJson));
    const credential = await navigator.credentials.get({ publicKey: options });
    return await postCredential(actionEndpointUrl, credential);
}

export async function createCredential(optionsEndpointUrl, actionEndpointUrl) {
    if (!isPasskeySupported) throw new Error('Passkeys are not supported in this browser.');
    const optionsJson = await fetchOptions(optionsEndpointUrl);
    const options = PublicKeyCredential.parseCreationOptionsFromJSON(JSON.parse(optionsJson));
    const credential = await navigator.credentials.create({ publicKey: options });
    return await postCredential(actionEndpointUrl, credential);
}

export async function startConditionalMediation(optionsEndpointUrl, actionEndpointUrl, dotNetRef) {
    if (!isPasskeySupported) return;
    if (!await isConditionalMediationAvailable()) return;

    cancelConditionalMediation();
    conditionalAbortController = new AbortController();

    try {
        const optionsJson = await fetchOptions(optionsEndpointUrl);
        const options = PublicKeyCredential.parseRequestOptionsFromJSON(JSON.parse(optionsJson));
        const credential = await navigator.credentials.get({
            publicKey: options,
            mediation: 'conditional',
            signal: conditionalAbortController.signal,
        });
        conditionalAbortController = null;
        const redirectUrl = await postCredential(actionEndpointUrl, credential);
        await dotNetRef.invokeMethodAsync('HandlePasskeyAutofill', redirectUrl);
    } catch (e) {
        if (e.name !== 'AbortError') {
            console.warn('Conditional passkey mediation error:', e);
        }
    }
}

export function cancelConditionalMediation() {
    if (conditionalAbortController) {
        conditionalAbortController.abort();
        conditionalAbortController = null;
    }
}
