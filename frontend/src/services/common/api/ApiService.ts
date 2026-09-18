const apiUrl = import.meta.env.VITE_API_URL;
const apiVersion = 'v1';

const get = async function (url: string, parameters: any = null): Promise<any> {

    const headers = {
    };

    if (parameters) {
        let count = 0;
        const keys = Object.keys(parameters);

        keys.forEach(key => {
            url += count == 0 ? '?' : '&';
            url += `${key}=${parameters[key]}`;
            count++;
        });
    }

    const request = fetch(`${apiUrl}/${apiVersion}/${url}`, {
        method: 'GET',
        mode: 'cors',
        headers: headers
    });

    const res = await request;
    if (!res.ok) throw new Error(`${res.status}`);

    return res.json();
}

const post = async function (url: string, body: any): Promise<Response> {
    const headers = {
        'Content-Type': 'application/json'
    };

    let request = fetch(`${apiUrl}/${apiVersion}/${url}`, {
        method: 'POST',
        mode: 'cors',
        headers: headers,
        body: JSON.stringify(body)
    });

    const res = await request;
    if (!res.ok) throw new Error(`${res.status}`);

    return res.json();
}

export { get, post }