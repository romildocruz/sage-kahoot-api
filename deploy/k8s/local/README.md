# Deploy local — k3s no WSL

Sobe a `sage-kahoot-api` e o `sage-kahoot-web` no k3s rodando dentro do WSL (Debian), no namespace
`sage-kahoot`, com o Traefik (ingress padrão do k3s). Para o deploy no cluster interno, use
`../k8sdeploy.yml`.

| Arquivo | Conteúdo |
|---|---|
| `api.yml` | ConfigMap, Deployment e Service da API |
| `web.yml` | ConfigMap, Deployment e Service do frontend |
| `ingress.yml` | Roteamento por caminho, mesma origem para os dois |

Web e API respondem no mesmo endereço (o IP do WSL), separados por caminho: `/` → web;
`/api`, `/hubs` e `/swagger` → API. Por isso o frontend roda com `API_BASE_URL` vazio e não há CORS
em jogo. Não se usa host por aplicação porque o DNS da rede local bloqueia `nip.io`.

Todos os comandos abaixo rodam **dentro do WSL**. O kubeconfig do k3s só é legível pelo root, daí o
`sudo` no `kubectl`.

## 1. MongoDB (fora do cluster)

```bash
docker run -d --name sage-kahoot-mongo --restart unless-stopped \
  -p 27017:27017 -v sage-kahoot-mongo-data:/data/db mongo:7.0
```

Os pods alcançam o Mongo pelo hostname do nó (`hostname` no WSL), que o k3s mantém no CoreDNS
mesmo quando o IP do WSL muda.

## 2. Imagens

Sem registry: as imagens são construídas no Docker do WSL e importadas no containerd do k3s.

```bash
cd /mnt/d/dev/sage-kahoot-api && docker build -t sage-kahoot-api:local .
cd /mnt/d/dev/sage-kahoot-web && docker build -t sage-kahoot-web:local .
docker save sage-kahoot-api:local sage-kahoot-web:local | sudo k3s ctr images import -
```

Se o pull falhar com `docker-credential-wincred.exe: executable file not found`, o
`~/.docker/config.json` do WSL aponta para o gerenciador de credenciais do Windows. Remova o
`credsStore` de lá ou rode os builds com `DOCKER_CONFIG` apontando para um diretório com `{}`.

## 3. Namespace e Secret

O Secret nunca vai para arquivo. Gere senha e chave na hora:

```bash
sudo kubectl create namespace sage-kahoot
sudo kubectl -n sage-kahoot create secret generic sage-kahoot-api-secret \
  --from-literal=Mongo__ConnectionString="mongodb://$(hostname):27017" \
  --from-literal=Auth__AdminUsername=admin \
  --from-literal=Auth__AdminPassword="$(openssl rand -base64 12)" \
  --from-literal=Auth__SigningKey="$(openssl rand -base64 48)"
```

Para consultar a senha do apresentador depois:

```bash
sudo kubectl -n sage-kahoot get secret sage-kahoot-api-secret \
  -o jsonpath='{.data.Auth__AdminPassword}' | base64 -d; echo
```

## 4. Aplicar

```bash
cd /mnt/d/dev/sage-kahoot-api/deploy/k8s/local
sudo kubectl apply -f api.yml -f web.yml -f ingress.yml
sudo kubectl -n sage-kahoot rollout status deploy/sage-kahoot-api
```

Acesso pelo IP do WSL (`hostname -I`): `http://<ip>/` (web) e `http://<ip>/swagger`.

## Atualizar após mudança de código

Refaça o passo 2 e reinicie o deployment — a tag `:local` não muda, então o `apply` sozinho não
troca o pod:

```bash
sudo kubectl -n sage-kahoot rollout restart deploy/sage-kahoot-api deploy/sage-kahoot-web
```

O reinício da API derruba as sessões ao vivo (SignalR sem backplane, estratégia `Recreate`).

## Pausar

```bash
sudo kubectl -n sage-kahoot scale deploy --all --replicas=0
```
